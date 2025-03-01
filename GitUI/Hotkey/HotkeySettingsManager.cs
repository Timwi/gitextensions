using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using GitCommands;
using GitUI.CommandsDialogs;
using GitUI.Editor;
using GitUI.Script;
using Microsoft;
using ResourceManager;

namespace GitUI.Hotkey
{
    internal static class HotkeySettingsManager
    {
        #region Serializer
        private static XmlSerializer? _serializer;

        /// <summary>Lazy-loaded Serializer for HotkeySettings[].</summary>
        private static XmlSerializer Serializer => _serializer ??= new XmlSerializer(typeof(HotkeySettings[]), new[] { typeof(HotkeyCommand) });

        #endregion

        private static readonly HashSet<Keys> _usedKeys = new();

        /// <summary>
        /// Returns whether the hotkey is already assigned.
        /// </summary>
        public static bool IsUniqueKey(Keys keyData)
        {
            return _usedKeys.Contains(keyData);
        }

        public static HotkeyCommand[] LoadHotkeys(string name)
        {
            HotkeySettings settings = new();
            HotkeySettings scriptKeys = new();
            var allSettings = LoadSettings();

            UpdateUsedKeys(allSettings);

            foreach (var setting in allSettings)
            {
                if (setting.Name == name)
                {
                    settings = setting;
                }

                if (setting.Name == "Scripts")
                {
                    scriptKeys = setting;
                }
            }

            // append general hotkeys to every form
            Validates.NotNull(settings.Commands);
            Validates.NotNull(scriptKeys.Commands);
            var allKeys = new HotkeyCommand[settings.Commands.Length + scriptKeys.Commands.Length];
            settings.Commands.CopyTo(allKeys, 0);
            scriptKeys.Commands.CopyTo(allKeys, settings.Commands.Length);

            return allKeys;
        }

        public static HotkeySettings[] LoadSettings()
        {
            // Get the default settings
            var defaultSettings = CreateDefaultSettings();
            var loadedSettings = LoadSerializedSettings();

            MergeIntoDefaultSettings(defaultSettings, loadedSettings);

            return defaultSettings;
        }

        private static void UpdateUsedKeys(HotkeySettings[] settings)
        {
            _usedKeys.Clear();

            foreach (var setting in settings)
            {
                if (setting.Commands is not null)
                {
                    foreach (var command in setting.Commands)
                    {
                        _usedKeys.Add(command.KeyData);
                    }
                }
            }
        }

        /// <summary>Serializes and saves the supplied settings.</summary>
        public static void SaveSettings(HotkeySettings[] settings)
        {
            try
            {
                UpdateUsedKeys(settings);

                StringBuilder str = new();
                using StringWriter writer = new(str);
                Serializer.Serialize(writer, settings);
                AppSettings.SerializedHotkeys = str.ToString();
            }
            catch
            {
                // ignore
            }
        }

        internal static void MergeIntoDefaultSettings(HotkeySettings[] defaultSettings, HotkeySettings[]? loadedSettings)
        {
            if (loadedSettings is null)
            {
                return;
            }

            Dictionary<string, HotkeyCommand> defaultCommands = new();

            FillDictionaryWithCommands();
            AssignHotkeysFromLoaded();

            void AssignHotkeysFromLoaded()
            {
                foreach (var setting in loadedSettings)
                {
                    if (setting.Commands is not null && setting.Name is not null)
                    {
                        foreach (var command in setting.Commands)
                        {
                            string dictKey = CalcDictionaryKey(setting.Name, command.CommandCode);
                            if (defaultCommands.TryGetValue(dictKey, out var defaultCommand))
                            {
                                defaultCommand.KeyData = command.KeyData;
                            }
                        }
                    }
                }
            }

            void FillDictionaryWithCommands()
            {
                foreach (var setting in defaultSettings)
                {
                    if (setting.Commands is not null && setting.Name is not null)
                    {
                        foreach (var command in setting.Commands)
                        {
                            string dictKey = CalcDictionaryKey(setting.Name, command.CommandCode);
                            defaultCommands.Add(dictKey, command);
                        }
                    }
                }
            }

            string CalcDictionaryKey(string settingName, int commandCode) => settingName + ":" + commandCode;
        }

        private static HotkeySettings[]? LoadSerializedSettings()
        {
            MigrateSettings();

            if (!string.IsNullOrWhiteSpace(AppSettings.SerializedHotkeys))
            {
                return LoadSerializedSettings(AppSettings.SerializedHotkeys);
            }

            return null;
        }

        private static HotkeySettings[]? LoadSerializedSettings(string serializedHotkeys)
        {
            try
            {
                using StringReader reader = new(serializedHotkeys);
                return (HotkeySettings[])Serializer.Deserialize(reader);
            }
            catch
            {
                return null;
            }
        }

        private static void MigrateSettings()
        {
            if (AppSettings.SerializedHotkeys is null)
            {
                Properties.Settings.Default.Upgrade();
                if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Hotkeys))
                {
                    HotkeySettings[]? settings = LoadSerializedSettings(Properties.Settings.Default.Hotkeys);
                    if (settings is null)
                    {
                        AppSettings.SerializedHotkeys = " "; // mark settings as migrated
                    }
                    else
                    {
                        SaveSettings(settings);
                    }
                }
                else
                {
                    AppSettings.SerializedHotkeys = " "; // mark settings as migrated
                }
            }
        }

        public static HotkeySettings[] CreateDefaultSettings()
        {
            HotkeyCommand Hk(object en, Keys k) => new((int)en, en.ToString()) { KeyData = k };

            const Keys OpenWithDifftoolHotkey = Keys.F3;
            const Keys OpenWithDifftoolFirstToLocalHotkey = Keys.Alt | Keys.F3;
            const Keys OpenWithDifftoolSelectedToLocalHotkey = Keys.Shift | Keys.Alt | Keys.F3;
            const Keys OpenAsTempFileHotkey = Keys.Control | Keys.F3;
            const Keys OpenAsTempFileWithHotkey = Keys.Shift | Keys.Control | Keys.F3;
            const Keys EditFileHotkey = Keys.F4;
            const Keys OpenFileHotkey = Keys.Shift | Keys.F4;
            const Keys OpenFileWithHotkey = Keys.Shift | Keys.Control | Keys.F4;
            const Keys ShowHistoryHotkey = Keys.H;
            const Keys BlameHotkey = Keys.B;

            return new[]
            {
                new HotkeySettings(
                    FormCommit.HotkeySettingsName,
                    Hk(FormCommit.Command.AddSelectionToCommitMessage, Keys.C),
                    Hk(FormCommit.Command.AddToGitIgnore, Keys.None),
                    Hk(FormCommit.Command.DeleteSelectedFiles, Keys.Delete),
                    Hk(FormCommit.Command.EditFile, EditFileHotkey),
                    Hk(FormCommit.Command.FocusUnstagedFiles, Keys.Control | Keys.D1),
                    Hk(FormCommit.Command.FocusSelectedDiff, Keys.Control | Keys.D2),
                    Hk(FormCommit.Command.FocusStagedFiles, Keys.Control | Keys.D3),
                    Hk(FormCommit.Command.FocusCommitMessage, Keys.Control | Keys.D4),
                    Hk(FormCommit.Command.OpenFile, OpenFileHotkey),
                    Hk(FormCommit.Command.OpenFileWith, OpenFileWithHotkey),
                    Hk(FormCommit.Command.OpenWithDifftool, OpenWithDifftoolHotkey),
                    Hk(FormCommit.Command.ResetSelectedFiles, Keys.R),
                    Hk(FormCommit.Command.StageSelectedFile, Keys.S),
                    Hk(FormCommit.Command.UnStageSelectedFile, Keys.U),
                    Hk(FormCommit.Command.ShowHistory, ShowHistoryHotkey),
                    Hk(FormCommit.Command.StageAll, Keys.Control | Keys.S),
                    Hk(FormCommit.Command.ToggleSelectionFilter, Keys.Control | Keys.F)),
                new HotkeySettings(
                    FormBrowse.HotkeySettingsName,
                    Hk(FormBrowse.Command.AddNotes, Keys.Control | Keys.Shift | Keys.N),
                    Hk(FormBrowse.Command.CheckoutBranch, Keys.Control | Keys.Decimal),
                    Hk(FormBrowse.Command.CloseRepository, Keys.Control | Keys.W),
                    Hk(FormBrowse.Command.Commit, Keys.Control | Keys.Space),
                    Hk(FormBrowse.Command.CreateBranch, Keys.Control | Keys.B),
                    Hk(FormBrowse.Command.CreateTag, Keys.Control | Keys.T),
                    Hk(FormBrowse.Command.EditFile, EditFileHotkey),
                    Hk(FormBrowse.Command.FindFileInSelectedCommit, Keys.Control | Keys.Shift | Keys.F),
                    Hk(FormBrowse.Command.FocusBranchTree, Keys.Control | Keys.D0),
                    Hk(FormBrowse.Command.FocusRevisionGrid, Keys.Control | Keys.D1),
                    Hk(FormBrowse.Command.FocusCommitInfo, Keys.Control | Keys.D2),
                    Hk(FormBrowse.Command.FocusDiff, Keys.Control | Keys.D3),
                    Hk(FormBrowse.Command.FocusFileTree, Keys.Control | Keys.D4),
                    Hk(FormBrowse.Command.FocusGpgInfo, Keys.Control | Keys.D5),
                    Hk(FormBrowse.Command.FocusGitConsole, Keys.Control | Keys.D6),
                    Hk(FormBrowse.Command.FocusBuildServerStatus, Keys.Control | Keys.D7),
                    Hk(FormBrowse.Command.FocusNextTab, Keys.Control | Keys.Tab),
                    Hk(FormBrowse.Command.FocusPrevTab, Keys.Control | Keys.Shift | Keys.Tab),
                    Hk(FormBrowse.Command.FocusFilter, Keys.Control | Keys.E),
                    Hk(FormBrowse.Command.GitBash, Keys.Control | Keys.G),
                    Hk(FormBrowse.Command.GitGui, Keys.None),
                    Hk(FormBrowse.Command.GitGitK, Keys.None),
                    Hk(FormBrowse.Command.GoToChild, Keys.Control | Keys.N),
                    Hk(FormBrowse.Command.GoToParent, Keys.Control | Keys.P),
                    Hk(FormBrowse.Command.GoToSubmodule, Keys.None),
                    Hk(FormBrowse.Command.GoToSuperproject, Keys.None),
                    Hk(FormBrowse.Command.MergeBranches, Keys.Control | Keys.M),
                    Hk(FormBrowse.Command.OpenAsTempFile, OpenAsTempFileHotkey),
                    Hk(FormBrowse.Command.OpenAsTempFileWith, OpenAsTempFileWithHotkey),
                    Hk(FormBrowse.Command.OpenCommitsWithDifftool, Keys.None),
                    Hk(FormBrowse.Command.OpenSettings, Keys.Control | Keys.Oemcomma),
                    Hk(FormBrowse.Command.OpenWithDifftool, OpenWithDifftoolHotkey),
                    Hk(FormBrowse.Command.OpenWithDifftoolFirstToLocal, OpenWithDifftoolFirstToLocalHotkey),
                    Hk(FormBrowse.Command.OpenWithDifftoolSelectedToLocal, OpenWithDifftoolSelectedToLocalHotkey),
                    Hk(FormBrowse.Command.PullOrFetch, Keys.Control | Keys.Down),
                    Hk(FormBrowse.Command.Push, Keys.Control | Keys.Up),
                    Hk(FormBrowse.Command.QuickFetch, Keys.Control | Keys.Shift | Keys.Down),
                    Hk(FormBrowse.Command.QuickPull, Keys.Control | Keys.Shift | Keys.P),
                    Hk(FormBrowse.Command.QuickPush, Keys.Control | Keys.Shift | Keys.Up),
                    Hk(FormBrowse.Command.Rebase, Keys.Control | Keys.Shift | Keys.E),
                    Hk(FormBrowse.Command.Stash, Keys.Control | Keys.Alt | Keys.Up),
                    Hk(FormBrowse.Command.StashPop, Keys.Control | Keys.Alt | Keys.Down),
                    Hk(FormBrowse.Command.ToggleBetweenArtificialAndHeadCommits, Keys.Control | Keys.OemBackslash),
                    Hk(FormBrowse.Command.ToggleBranchTreePanel, Keys.Control | Keys.Alt | Keys.C)),
                new HotkeySettings(
                    RevisionGridControl.HotkeySettingsName,
                    Hk(RevisionGridControl.Command.CompareSelectedCommits, Keys.None),
                    Hk(RevisionGridControl.Command.CompareToBase, Keys.Control | Keys.R),
                    Hk(RevisionGridControl.Command.CompareToBranch, Keys.None),
                    Hk(RevisionGridControl.Command.CompareToCurrentBranch, Keys.None),
                    Hk(RevisionGridControl.Command.CompareToWorkingDirectory, Keys.Control | Keys.D),
                    Hk(RevisionGridControl.Command.CreateFixupCommit, Keys.Control | Keys.X),
                    Hk(RevisionGridControl.Command.GoToChild, Keys.Control | Keys.N),
                    Hk(RevisionGridControl.Command.GoToCommit, Keys.Control | Keys.Shift | Keys.G),
                    Hk(RevisionGridControl.Command.GoToParent, Keys.Control | Keys.P),
                    Hk(RevisionGridControl.Command.NextQuickSearch, Keys.Alt | Keys.Down),
                    Hk(RevisionGridControl.Command.OpenCommitsWithDifftool, Keys.None),
                    Hk(RevisionGridControl.Command.PrevQuickSearch, Keys.Alt | Keys.Up),
                    Hk(RevisionGridControl.Command.RevisionFilter, Keys.Control | Keys.F),
                    Hk(RevisionGridControl.Command.SelectCurrentRevision, Keys.Control | Keys.Shift | Keys.C),
                    Hk(RevisionGridControl.Command.SelectAsBaseToCompare, Keys.Control | Keys.L),
                    Hk(RevisionGridControl.Command.ShowAllBranches, Keys.Control | Keys.Shift | Keys.A),
                    Hk(RevisionGridControl.Command.ShowCurrentBranchOnly, Keys.Control | Keys.Shift | Keys.U),
                    Hk(RevisionGridControl.Command.ShowFilteredBranches, Keys.Control | Keys.Shift | Keys.T),
                    Hk(RevisionGridControl.Command.ShowFirstParent, Keys.Control | Keys.Shift | Keys.S),
                    Hk(RevisionGridControl.Command.ShowReflogReferences, Keys.Control | Keys.Shift | Keys.L),
                    Hk(RevisionGridControl.Command.ShowRemoteBranches, Keys.Control | Keys.Shift | Keys.R),
                    Hk(RevisionGridControl.Command.ToggleAuthorDateCommitDate, Keys.None),
                    Hk(RevisionGridControl.Command.ToggleBetweenArtificialAndHeadCommits, Keys.Control | Keys.OemBackslash),
                    Hk(RevisionGridControl.Command.ToggleDrawNonRelativesGray, Keys.None),
                    Hk(RevisionGridControl.Command.ToggleHighlightSelectedBranch, Keys.Control | Keys.Shift | Keys.B),
                    Hk(RevisionGridControl.Command.ToggleOrderRevisionsByDate, Keys.None),
                    Hk(RevisionGridControl.Command.ToggleRevisionGraph, Keys.None),
                    Hk(RevisionGridControl.Command.ToggleShowGitNotes, Keys.None),
                    Hk(RevisionGridControl.Command.ToggleShowMergeCommits, Keys.Control | Keys.Shift | Keys.M),
                    Hk(RevisionGridControl.Command.ToggleShowRelativeDate, Keys.None),
                    Hk(RevisionGridControl.Command.ToggleShowTags, Keys.Control | Keys.Alt | Keys.T)),
                new HotkeySettings(
                    FileViewer.HotkeySettingsName,
                    Hk(FileViewer.Command.Find, Keys.Control | Keys.F),
                    Hk(FileViewer.Command.FindNextOrOpenWithDifftool, OpenWithDifftoolHotkey),
                    Hk(FileViewer.Command.FindPrevious, Keys.Shift | OpenWithDifftoolHotkey),
                    Hk(FileViewer.Command.GoToLine, Keys.Control | Keys.G),
                    Hk(FileViewer.Command.IncreaseNumberOfVisibleLines, Keys.None),
                    Hk(FileViewer.Command.DecreaseNumberOfVisibleLines, Keys.None),
                    Hk(FileViewer.Command.NextChange, Keys.Alt | Keys.Down),
                    Hk(FileViewer.Command.PreviousChange, Keys.Alt | Keys.Up),
                    Hk(FileViewer.Command.ShowEntireFile, Keys.None),
                    Hk(FileViewer.Command.TreatFileAsText, Keys.None),
                    Hk(FileViewer.Command.NextOccurrence, Keys.Alt | Keys.Right),
                    Hk(FileViewer.Command.PreviousOccurrence, Keys.Alt | Keys.Left),
                    Hk(FileViewer.Command.StageLines, Keys.S),
                    Hk(FileViewer.Command.UnstageLines, Keys.U),
                    Hk(FileViewer.Command.ResetLines, Keys.R)),
                new HotkeySettings(
                    FormResolveConflicts.HotkeySettingsName,
                    Hk(FormResolveConflicts.Commands.ChooseBase, Keys.B),
                    Hk(FormResolveConflicts.Commands.ChooseLocal, Keys.L),
                    Hk(FormResolveConflicts.Commands.ChooseRemote, Keys.R),
                    Hk(FormResolveConflicts.Commands.Merge, Keys.M),
                    Hk(FormResolveConflicts.Commands.Rescan, Keys.F5)),
                new HotkeySettings(
                    RevisionDiffControl.HotkeySettingsName,
                    Hk(RevisionDiffControl.Command.Blame, BlameHotkey),
                    Hk(RevisionDiffControl.Command.DeleteSelectedFiles, Keys.Delete),
                    Hk(RevisionDiffControl.Command.EditFile, EditFileHotkey),
                    Hk(RevisionDiffControl.Command.OpenAsTempFile, OpenAsTempFileHotkey),
                    Hk(RevisionDiffControl.Command.OpenAsTempFileWith, OpenAsTempFileWithHotkey),
                    Hk(RevisionDiffControl.Command.OpenWithDifftool, OpenWithDifftoolHotkey),
                    Hk(RevisionDiffControl.Command.OpenWithDifftoolFirstToLocal, OpenWithDifftoolFirstToLocalHotkey),
                    Hk(RevisionDiffControl.Command.OpenWithDifftoolSelectedToLocal, OpenWithDifftoolSelectedToLocalHotkey),
                    Hk(RevisionDiffControl.Command.ShowHistory, ShowHistoryHotkey),
                    Hk(RevisionDiffControl.Command.ResetSelectedFiles, Keys.R),
                    Hk(RevisionDiffControl.Command.StageSelectedFile, Keys.S),
                    Hk(RevisionDiffControl.Command.UnStageSelectedFile, Keys.U),
                    Hk(RevisionDiffControl.Command.ShowFileTree, Keys.T),
                    Hk(RevisionDiffControl.Command.FilterFileInGrid, Keys.F)),
                new HotkeySettings(
                    RevisionFileTreeControl.HotkeySettingsName,
                    Hk(RevisionFileTreeControl.Command.Blame, BlameHotkey),
                    Hk(RevisionFileTreeControl.Command.EditFile, EditFileHotkey),
                    Hk(RevisionFileTreeControl.Command.OpenAsTempFile, OpenAsTempFileHotkey),
                    Hk(RevisionFileTreeControl.Command.OpenAsTempFileWith, OpenAsTempFileWithHotkey),
                    Hk(RevisionFileTreeControl.Command.OpenWithDifftool, OpenWithDifftoolHotkey),
                    Hk(RevisionFileTreeControl.Command.ShowHistory, ShowHistoryHotkey),
                    Hk(RevisionFileTreeControl.Command.FilterFileInGrid, Keys.F)),
                new HotkeySettings(
                    FormStash.HotkeySettingsName,
                    Hk(FormStash.Command.NextStash, Keys.Control | Keys.N),
                    Hk(FormStash.Command.PreviousStash, Keys.Control | Keys.P),
                    Hk(FormStash.Command.Refresh, Keys.F5)),
                new HotkeySettings(
                    FormSettings.HotkeySettingsName,
                    LoadScriptHotkeys())
            };

            HotkeyCommand[] LoadScriptHotkeys()
            {
                /* define unusable int for identifying a shortcut for a custom script is pressed
                 * all integers above 9000 represent a script hotkey
                 * these integers are never matched in the 'switch' routine on a form and
                 * therefore execute the 'default' action
                 */
                return ScriptManager
                    .GetScripts()
                    .Where(s => !string.IsNullOrEmpty(s.Name))
                    .Select(s => new HotkeyCommand(s.HotkeyCommandIdentifier, s.Name!) { KeyData = Keys.None })
                    .ToArray();
            }
        }
    }
}
