using ClickableTransparentOverlay;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Mathematics;

namespace Chrome2
{
    public class Renderer : Overlay
    {
        public enum SnaplineOrigin
        {
            Bottom,
            Crosshair
        }

        private Entity localplayer = new Entity();
        public Vector2 screenSize = new Vector2(1920, 1080);
        private ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private readonly object entityLock = new object();
        public Vector4 enemyColor = new Vector4(1, 0, 0, 1);
        public Vector4 teamColor = new Vector4(0, 1, 0, 1);
        ImDrawListPtr drawList;
        private Vector2 windowSize = new Vector2(300, 300);
        private Vector2 windowPos = new Vector2(100, 100);
        public bool espBox = true;
        public bool espSnaplines = true;
        public SnaplineOrigin snaplineOrigin = SnaplineOrigin.Bottom;
        public bool aimbotEnabled = false;
        public float aimbotSmooth = 0.5f;
        public float aimbotFOV = 90f;
        public bool bunnyhop = false;
        public bool antiflash = true;
        public bool hideTeam = true;
        public bool triggerBotEnabled = true;
        public int triggerDelay = 10;

        protected override void Render()
        {
            RenderUI();
            RenderESP();
        }

        private void RenderUI()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.12f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.2f, 0.0f, 0.5f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.3f, 0.1f, 0.6f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.2f, 0.2f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.3f, 0.3f, 0.6f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.5f, 0.5f, 0.9f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6.0f);

            ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
            ImGui.SetNextWindowPos(windowPos, ImGuiCond.FirstUseEver);

            ImGui.Begin("Ecternal - CS2", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar);
            ImGui.BeginChild("Header", new Vector2(0, 30));
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 1f, 1f), "   Ecternal CS2 Overlay");

            ImGui.SameLine(ImGui.GetWindowWidth() - 30);
            if (ImGui.Button("X"))
                Environment.Exit(0);
            ImGui.EndChild();

            if (ImGui.BeginTabBar("MainTabs"))
            {
                if (ImGui.BeginTabItem("ESP"))
                {
                    ImGui.Text("Toggle ESP features:");
                    ImGui.Checkbox("Box ESP", ref espBox);
                    ImGui.Checkbox("Snaplines", ref espSnaplines);

                    string[] snaplineOptions = { "Bottom", "Crosshair" };
                    int selectedSnaplineOrigin = (int)snaplineOrigin;

                    ImGui.Text("Snapline Origin:");
                    if (ImGui.Combo("##SnaplineOrigin", ref selectedSnaplineOrigin, snaplineOptions, snaplineOptions.Length))
                    {
                        snaplineOrigin = (SnaplineOrigin)selectedSnaplineOrigin;
                    }

                    ImGui.Spacing();
                    ImGui.Text("ESP Colors:");
                    ImGui.ColorEdit4("Enemy Color", ref enemyColor);
                    ImGui.ColorEdit4("Team Color", ref teamColor);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Aimbot"))
                {
                    ImGui.Text("Aimbot settings:");
                    ImGui.Checkbox("Enable Aimbot", ref aimbotEnabled);
                    ImGui.SliderFloat("Smoothness", ref aimbotSmooth, 0f, 1f);
                    ImGui.SliderFloat("FOV", ref aimbotFOV, 0f, 180f);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Misc"))
                {
                    ImGui.Text("Misc settings:");
                    ImGui.Checkbox("Bunnyhop", ref bunnyhop);
                    ImGui.Checkbox("Anti-Flash", ref antiflash);
                    ImGui.Checkbox("TeamCheck", ref hideTeam);

                    ImGui.Separator();
                    ImGui.Text("Trigger Bot:");
                    ImGui.Checkbox("Enable Trigger Bot", ref triggerBotEnabled);
                    ImGui.SliderInt("Trigger Delay (ms)", ref triggerDelay, 10, 250);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("About"))
                {
                    ImGui.TextWrapped("Ecternal - External CS2 Overlay");
                    ImGui.Text("Made by KronixYT");
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            drawList = ImGui.GetWindowDrawList();
            ImGui.End();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(6);
        }

        private void RenderESP()
        {
            foreach (var entity in entities)
            {
                if (!EntityOnScreen(entity))
                    continue;

                if (entity.team == localplayer.team && hideTeam)
                    continue;

                Vector4 color = localplayer.team == entity.team ? teamColor : enemyColor;

                Vector2 feet = entity.position2D;
                Vector2 head = entity.viewPosition2D;

                if (espBox)
                    DrawBox(feet, head, color);

                if (espSnaplines)
                    DrawLine(feet, color);
            }
        }

        private bool EntityOnScreen(Entity entity)
        {
            return entity.position2D.X > 0 && entity.position2D.X < screenSize.X &&
                   entity.position2D.Y > 0 && entity.position2D.Y < screenSize.Y;
        }

        private void DrawBox(Vector2 feet, Vector2 head, Vector4 color)
        {
            float height = feet.Y - head.Y;
            float width = height / 2f;

            Vector2 topLeft = new Vector2(head.X - width / 2f, head.Y);
            Vector2 bottomRight = new Vector2(head.X + width / 2f, feet.Y);

            drawList.AddRect(topLeft, bottomRight, ImGui.ColorConvertFloat4ToU32(color), 0f, ImDrawFlags.None, 2f);
        }

        private void DrawLine(Vector2 target, Vector4 color)
        {
            Vector2 start;

            switch (snaplineOrigin)
            {
                case SnaplineOrigin.Crosshair:
                    start = new Vector2(screenSize.X / 2f, screenSize.Y / 2f);
                    break;
                case SnaplineOrigin.Bottom:
                default:
                    start = new Vector2(screenSize.X / 2f, screenSize.Y);
                    break;
            }

            drawList.AddLine(start, target, ImGui.ColorConvertFloat4ToU32(color), 1f);
        }

        public void UpdateEntities(IEnumerable<Entity> newEntities)
        {
            entities = new ConcurrentQueue<Entity>(newEntities);
        }

        public void UpdateLocalPlayer(Entity newEntity)
        {
            lock (entityLock)
            {
                localplayer = newEntity;
            }
        }

        public Entity GetLocalPlayer()
        {
            lock (entityLock)
            {
                return localplayer;
            }
        }
    }
}
