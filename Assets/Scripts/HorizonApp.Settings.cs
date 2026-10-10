using System;
using System.IO;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private ArchiveStore saveStore;
        private bool backRequested, settingsVisible, userPaused;
        private ArchiveData importCandidate;
        private string importCode;
        private bool storageNoticeShown;

        private void ShowStorageNotice()
        {
            storageNoticeShown = true;
            overlay = View.Rect(root, "Storage recovery", 0, 0, 1, 1);
            View.Fill(overlay, "Storage recovery shade", new Color(0.01f, 0.026f, 0.045f, 0.98f), 0, 0, 1, 1, true);
            View.Label(overlay, "Recovery title", "你的人生记录", 43, Palette.Text, TextAnchor.MiddleCenter,
                0.075f, 0.63f, 0.925f, 0.72f);
            View.Label(overlay, "Recovery explanation", saveStore.Notice, 30, Palette.Muted, TextAnchor.MiddleCenter,
                0.09f, 0.40f, 0.91f, 0.60f);
            View.Button(overlay, "Close storage notice", "我知道了", () =>
                { Destroy(overlay.gameObject); overlay = null; }, 0.15f, 0.27f, 0.85f, 0.35f, Palette.Mint, Palette.Ink, 32);
        }

        private void ApplyPreferences()
        {
            VisualPreferences.ReducedMotion = archive.preferences.reducedMotion;
            Application.targetFrameRate = archive.preferences.batterySaver ? 30 : 60;
            if (world != null) world.ApplyPreferences(archive.preferences);
        }

        private void SetPaused(bool paused)
        {
            userPaused = paused;
            VisualPreferences.Paused = paused;
            if (world != null) world.SetPaused(paused);
        }

        private void Haptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (archive.preferences.haptics) Handheld.Vibrate();
#endif
        }

        private void PersistLiveLife()
        {
            if (session != null && session.CompletedRun == null && archive.active?.runNumber == session.RunNumber)
                archive.active = session.Snapshot();
            Save();
        }

        private void OnApplicationPause(bool paused)
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD
            AndroidSmoke.NotePause(paused);
#endif
            if (paused) { CancelAIRequest(); PersistLiveLife(); }
            if (world != null) world.SetPaused(paused || userPaused);
        }

        private void OnApplicationQuit() { CancelAIRequest(); aiSecrets.Clear(); PersistLiveLife(); }
        private void OnDestroy() { CancelAIRequest(); aiSecrets.Clear(); }

        private void HandleBack()
        {
            if (busy) { backRequested = true; return; }
            if (overlay != null)
            {
                Button close = overlay.GetComponentsInChildren<Button>().Reverse().FirstOrDefault(b =>
                    b.interactable && (b.name.StartsWith("Close ") || b.name.StartsWith("Back ") ||
                    b.name == "Close" || b.name == "Cancel" || b.name == "Leave story" || b.name == "Master back" ||
                    b.name == "Continue master event"));
                if (close != null) { close.onClick.Invoke(); return; }
                CloseMasterPage(); world.Focus(false); return;
            }
            ShowSettings();
        }

        private void ShowSettings()
        {
            if (busy || overlay != null || activeDrag != null) return;
            settingsVisible = true; SetPaused(true); PersistLiveLife(); RenderSettings();
        }

        private RectTransform SettingsPanel(string title, string subtitle)
        {
            CancelAIRequest();
            if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
            overlay = View.Rect(root, "Product settings", 0, 0, 1, 1);
            View.Fill(overlay, "Settings shade", new Color(0.008f, 0.022f, 0.04f, 0.985f), 0, 0, 1, 1, true);
            View.Label(overlay, "Settings heading", title, 43, Palette.Text, TextAnchor.MiddleLeft,
                0.075f, 0.866f, 0.925f, 0.934f);
            View.Label(overlay, "Settings subtitle", subtitle, 25, Palette.Muted, TextAnchor.MiddleLeft,
                0.075f, 0.808f, 0.925f, 0.86f);
            return overlay;
        }

        private void RenderSettings()
        {
            SettingsPanel("留一会儿，再继续", saveStore.WriteBlocked ? "当前游戏暂停 · 新版本存档已保留" :
                saveStore.Notice == null ? "人生已保存 · 当前游戏暂停" : "当前游戏暂停 · 存档情况见下方");
            PlayerPreferences p = archive.preferences;
            SettingRow(0, "声音", "行动与回声的提示音", p.sound, () => p.sound = !p.sound);
            SettingRow(1, "环境音乐", "安静的地平线与未来站", p.ambience, () => p.ambience = !p.ambience);
            SettingRow(2, "震动", "接住卡牌、回声抵达时的触感", p.haptics, () => p.haptics = !p.haptics);
            SettingRow(3, "减弱动效", "关闭闪屏与镜头抖动，减少粒子", p.reducedMotion, () => p.reducedMotion = !p.reducedMotion);
            SettingRow(4, "省电画质", "30帧，关闭光晕与实时阴影", p.batterySaver, () => p.batterySaver = !p.batterySaver);
            View.Button(overlay, "Life backups", "备份 / 导入人生", RenderBackupTools,
                0.075f, 0.269f, 0.49f, 0.332f, Palette.Panel, Palette.Text, 24);
            View.Button(overlay, "Online AI settings", "在线 AI", () => { aiSettingsReturnToFuture = false; ShowAISettings(); },
                0.51f, 0.269f, 0.925f, 0.332f, Palette.Panel, Palette.Mint, 26);
            string status = saveStore.Notice ?? "自动保留当前与上一份安全存档。";
            View.Label(overlay, "Storage status", status, 22, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.183f, 0.67f, 0.25f);
            View.Button(overlay,"Performance report","运行表现",ShowPerformance,.7f,.192f,.925f,.248f,Palette.Panel,Palette.Mint,23);
            View.Button(overlay, "Close settings", "继续这段人生", CloseSettings,
                0.075f, 0.085f, 0.925f, 0.161f, Palette.Mint, Palette.Ink, 31);
            if (archive.runs.Count > 0) View.Button(overlay, "Home from settings", "回到地平线", () =>
                { CloseSettings(); ShowHome(); }, 0.075f, 0.018f, 0.49f, 0.068f, Palette.Deep, Palette.Muted, 23);
            if (archive.runs.Count > 0) View.Button(overlay, "Quit saved life", "保存并离开", () =>
                { PersistLiveLife(); Application.Quit(); }, 0.51f, 0.018f, 0.925f, 0.068f, Palette.Deep, Palette.Muted, 23);
            else View.Button(overlay, "Quit saved life", "保存并离开", () => { PersistLiveLife(); Application.Quit(); },
                0.13f, 0.018f, 0.87f, 0.068f, Palette.Deep, Palette.Muted, 23);
            View.RefreshText(overlay);
        }

        private void SettingRow(int index, string title, string detail, bool value, Action toggle)
        {
            float y = 0.714f - index * 0.09f;
            View.Panel(overlay, title + " setting row", Palette.Panel, 0.075f, y, 0.925f, y + 0.079f, 22);
            View.Label(overlay, title + " title", title, 29, Palette.Text, TextAnchor.MiddleLeft,
                0.105f, y + 0.034f, 0.68f, y + 0.074f);
            View.Label(overlay, title + " detail", detail, 21, Palette.Muted, TextAnchor.MiddleLeft,
                0.105f, y + 0.005f, 0.705f, y + 0.036f);
            View.Button(overlay, "Toggle " + index, value ? "开启" : "关闭", () =>
                { toggle(); ApplyPreferences(); Save(); RenderSettings(); },
                0.73f, y + 0.012f, 0.9f, y + 0.067f, value ? Palette.Mint : Palette.Deep,
                value ? Palette.Ink : Palette.Muted, 25);
        }

        private void CloseSettings()
        {
            CancelAIRequest();
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null; settingsVisible = false; SetPaused(false); ApplyPreferences();
        }

        private void RenderBackupTools()
        {
            SettingsPanel("把走过的人生留好", "备份包含时间地图、星尘、进行中的一天与阅读位置。");
            View.Label(overlay, "Backup summary", "已收藏 " + archive.runs.Count + " 段人生\n" +
                (archive.active == null ? "此刻没有进行中的人生" : "正在第 " + archive.active.day + " 天") +
                " · 星尘 " + archive.wallet.stardust, 34, Palette.Mint, TextAnchor.MiddleLeft,
                0.075f, 0.665f, 0.925f, 0.78f);
            Text result = View.Label(overlay, "Backup result", "将备份文本保存在你的笔记中，之后可以粘贴回来。", 25,
                Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.475f, 0.925f, 0.585f);
            View.Button(overlay, "Copy life backup", "复制完整人生备份", () =>
            {
                try { string code = ArchiveStore.Encode(archive); GUIUtility.systemCopyBuffer = code;
                    File.WriteAllText(Path.Combine(Application.persistentDataPath, "HORIZON-export.json"), code);
                    result.text = "人生备份已复制。粘贴到笔记并妥善保存。"; }
                catch (Exception) { result.text = "备份未能完成，请检查设备空间或再试一次。"; }
            }, 0.075f, 0.592f, 0.925f, 0.656f, Palette.Panel, Palette.Gold, 29);
            View.Button(overlay, "Paste life backup", "从剪贴板读取备份", () => PrepareImport(GUIUtility.systemCopyBuffer),
                0.075f, 0.38f, 0.925f, 0.445f, Palette.Panel, Palette.Text, 29);
            if (File.Exists(saveStore.Path + ".before-import")) View.Button(overlay, "Rollback imported life", "恢复导入前的本机记录",
                () => PrepareImport(File.ReadAllText(saveStore.Path + ".before-import")),
                0.075f, 0.29f, 0.925f, 0.355f, Palette.Panel, Palette.Muted, 25);
            View.Label(overlay, "Backup guidance", "导入前会显示内容供你确认。\n更新试玩包时，也请先留一份自己的备份。", 24, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.18f, 0.925f, 0.26f);
            View.Button(overlay, "Back to settings", "返回设置", RenderSettings,
                0.075f, 0.085f, 0.925f, 0.16f, Palette.Mint, Palette.Ink, 31);
        }

        private void PrepareImport(string code)
        {
            ArchiveData candidate; string error;
            if (!ArchiveStore.TryDecode(code, out candidate, out error))
            {
                Text status = overlay.GetComponentsInChildren<Text>().FirstOrDefault(t => t.name == "Backup result");
                if (status != null) status.text = error == "newer" ? "这份备份需要更新版本才能打开。" : "没有读到完整备份。请先复制完整文本。";
                return;
            }
            importCandidate = candidate; importCode = code;
            SettingsPanel("导入这份人生备份？", "导入后，本机记录会替换为下面这份内容。");
            View.Label(overlay, "Import contents", "已收藏 " + candidate.runs.Count + " 段人生\n星尘 " + candidate.wallet.stardust +
                "\n" + (candidate.active == null ? "没有进行中的人生" : "继续 RUN " + candidate.active.runNumber + " · 第 " + candidate.active.day + " 天"),
                38, Palette.Mint, TextAnchor.MiddleLeft, 0.075f, 0.53f, 0.925f, 0.76f);
            View.Label(overlay, "Import safety", "导入前的本机记录会单独保留，之后可以恢复。", 26, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.405f, 0.925f, 0.49f);
            View.Button(overlay, "Confirm life import", "导入并继续", ConfirmImport,
                0.075f, 0.235f, 0.925f, 0.315f, Palette.Mint, Palette.Ink, 32);
            View.Button(overlay, "Back to backups", "取消，保留本机记录", RenderBackupTools,
                0.075f, 0.13f, 0.925f, 0.203f, Palette.Panel, Palette.Text, 29);
        }

        private void ConfirmImport()
        {
            if (importCandidate == null) return;
            ArchiveData imported; string error;
            if (!saveStore.Import(importCode, out imported, out error))
            { View.Label(overlay, "Import error", "这次未导入完成，本机记录仍保留。", 26, Palette.Coral,
                TextAnchor.MiddleCenter, 0.075f, 0.34f, 0.925f, 0.405f); return; }
            importCandidate = null; importCode = null;
            archive = imported; session = null; CloseSettings(); world.SetTheme(archive.wallet.theme);
            if (archive.active != null || archive.pendingFeedback != null) ContinueRun(); else ShowHome();
        }
    }
}
