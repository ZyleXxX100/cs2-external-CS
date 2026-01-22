using Chrome2;
using Swed64;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

Swed swed = new Swed("cs2");
IntPtr client = swed.GetModuleBase("client.dll");

Renderer renderer = new Renderer();
Thread renderThread = new Thread(() => renderer.Start().Wait());
renderThread.Start();

Vector2 screenSize = renderer.screenSize;

List<Entity> entities = new List<Entity>();
Entity localPlayer = new Entity();

// ==== Hotkeys ====
const int HOTKEY_AIMBOT = 0x05;     // Mouse button 4
const int HOTKEY_TRIGGER = 0x06;    // Mouse button 5

// ==== Offsets ====
int dwEntityList = 0x21C39F8;
int dwViewMatrix = 0x2307750;
// updated per your supplied list
int dwLocalPlayerPawn = 0x20617D0;
int dwViewAngles = 0x2311958;
// === button.cs
int attack = 0x205A560;
// ==== client.dll ====
int m_vOldOrigin = 0x1588;
int m_iTeamNum = 0x3F3;
int m_lifeState = 0x35C;
int m_hPlayerPawn = 0x90C;
int m_vecViewOffset = 0xD58;
int m_flFlashBangTime = 0x15E4;
int m_iHealth = 0x354;
int m_iIDEntIndex = 0x3EAC;

static float NormalizeAngle(float a)
{
    while (a > 180f) a -= 360f;
    while (a < -180f) a += 360f;
    return a;
}

// === Start Game Data Thread ===
new Thread(() =>
{
    const int DATA_FPS = 30;
    const int DATA_FRAME_TIME = 1000 / DATA_FPS;
    Stopwatch sw = new Stopwatch();

    while (true)
    {
        sw.Restart();

        IntPtr localPlayerPawn = swed.ReadPointer(client + dwLocalPlayerPawn);
        IntPtr entityList = swed.ReadPointer(client + dwEntityList);
        IntPtr listEntry = swed.ReadPointer(entityList, 0x10);
        IntPtr listEntry2 = swed.ReadPointer(entityList, 0x18); // <-- Added to fix CS0103

        if (localPlayerPawn == IntPtr.Zero || entityList == IntPtr.Zero || listEntry == IntPtr.Zero || listEntry2 == IntPtr.Zero)
        {
            Thread.Sleep(1);
            continue;
        }

        // === Anti-Flash ===
        float flashTime = swed.ReadFloat(localPlayerPawn + m_flFlashBangTime);
        if (renderer.antiflash && flashTime > 0f)
            swed.WriteFloat(localPlayerPawn + m_flFlashBangTime, 0f);

        // === Trigger Bot ===
        int localTeam = swed.ReadInt(localPlayerPawn, m_iTeamNum);
        int crosshairEntIndex = swed.ReadInt(localPlayerPawn, m_iIDEntIndex);

        if (crosshairEntIndex != -1)
        {
            IntPtr crossListEntry = swed.ReadPointer(entityList + 0x10 + 0x8 * ((crosshairEntIndex & 0x7FFF) >> 9));
            if (crossListEntry == IntPtr.Zero) continue;

            IntPtr crossEntity = swed.ReadPointer(crossListEntry + 0x70 * (crosshairEntIndex & 0x1FF));
            if (crossEntity == IntPtr.Zero) continue;

            if (crossEntity != IntPtr.Zero)
            {
                int targetTeam = swed.ReadInt(crossEntity, m_iTeamNum);
                if (targetTeam != localTeam && GetAsyncKeyState(HOTKEY_TRIGGER) < 0 && renderer.triggerBotEnabled)
                {
                    Thread.Sleep(renderer.triggerDelay);
                    swed.WriteInt(client + attack, 65537); // press
                    Thread.Sleep(10);
                    swed.WriteInt(client + attack, 256);   // release
                }
            }
        }

        // === ESP Entity Collection ===
        entities.Clear();
        float[] viewMatrix = swed.ReadMatrix(client + dwViewMatrix);
        localPlayer.team = localTeam;

        for (int i = 0; i < 64; i++)
        {
            IntPtr currentController = swed.ReadPointer(listEntry, i * 0x78);
            if (currentController == IntPtr.Zero) continue;

            int pawnHandle = swed.ReadInt(currentController + m_hPlayerPawn);
            if (pawnHandle == 0) continue;

            IntPtr currentPawn = swed.ReadPointer(listEntry2 + 0x70 * (pawnHandle & 0x1FF));
            if (currentPawn == IntPtr.Zero) continue;

            if (swed.ReadInt(currentPawn + m_lifeState) != 256) continue;

            Entity entity = new Entity
            {
                team = swed.ReadInt(currentPawn + m_iTeamNum),
                postion = swed.ReadVec(currentPawn + m_vOldOrigin),
                viewOffset = swed.ReadVec(currentPawn + m_vecViewOffset)
            };

            // pass the live overlay screen size (do not use a one-time copy)
            entity.position2D = Calculate.WorldToScreen(viewMatrix, entity.postion, renderer.screenSize);
            entity.viewPosition2D = Calculate.WorldToScreen(viewMatrix, entity.postion + entity.viewOffset, renderer.screenSize);

            if (entity.position2D != Vector2.Zero)
                entities.Add(entity);
        }

        // === Aimbot ===
        if (renderer.aimbotEnabled && GetAsyncKeyState(HOTKEY_AIMBOT) < 0)
        {
            // read local eye position
            Vector3 localOrigin = swed.ReadVec(localPlayerPawn + m_vOldOrigin);
            Vector3 localViewOffset = swed.ReadVec(localPlayerPawn + m_vecViewOffset);
            Vector3 localEye = localOrigin + localViewOffset;

            // read current view angles (pitch, yaw)
            float curPitch = swed.ReadFloat(client + dwViewAngles);
            float curYaw = swed.ReadFloat(client + dwViewAngles + 4);

            float bestFov = renderer.aimbotFOV;
            Vector2 bestAngles = Vector2.Zero;
            bool found = false;

            foreach (var e in entities)
            {
                if (e.team == localTeam) continue;

                Vector3 targetPos = e.postion + e.viewOffset;
                Vector3 delta = targetPos - localEye;

                float hyp = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                if (hyp < 0.001f) continue;

                float yaw = (float)(Math.Atan2(delta.Y, delta.X) * (180.0 / Math.PI));
                float pitch = (float)(-Math.Atan2(delta.Z, hyp) * (180.0 / Math.PI));

                // normalize differences
                float dy = NormalizeAngle(yaw - curYaw);
                float dp = NormalizeAngle(pitch - curPitch);

                float fov = (float)Math.Sqrt(dy * dy + dp * dp);
                if (fov < bestFov)
                {
                    bestFov = fov;
                    bestAngles = new Vector2(pitch, yaw);
                    found = true;
                }
            }

            if (found)
            {
                // smoothing: move a portion towards target angles
                float targetPitch = bestAngles.X;
                float targetYaw = bestAngles.Y;

                float dPitch = NormalizeAngle(targetPitch - curPitch);
                float dYaw = NormalizeAngle(targetYaw - curYaw);

                Vector2 smoothed = new Vector2(
                    curPitch + dPitch * renderer.aimbotSmooth,
                    curYaw + dYaw * renderer.aimbotSmooth
                );

                // clamp pitch to avoid extremes
                smoothed.X = Math.Max(-89f, Math.Min(89f, smoothed.X));

                // write new angles (pitch, yaw)
                swed.WriteFloat(client + dwViewAngles, smoothed.X);
                swed.WriteFloat(client + dwViewAngles + 4, smoothed.Y);
            }
        }

        renderer.UpdateLocalPlayer(localPlayer);
        renderer.UpdateEntities(entities);

        sw.Stop();
        int sleepTime = DATA_FRAME_TIME - (int)sw.ElapsedMilliseconds;
        if (sleepTime > 0)
            Thread.Sleep(sleepTime);
    }
}).Start();

[DllImport("user32.dll")]
static extern short GetAsyncKeyState(int vKey);
