﻿using System.Diagnostics;
using System.Drawing.Imaging;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.Avatars;

namespace GitExtensions.Plugins.Gource
{
    public partial class GourceStart : ResourceManager.GitExtensionsFormBase
    {
        public GourceStart(string pathToGource, GitUIEventArgs gitUIArgs, string gourceArguments)
        {
            InitializeComponent();
            InitializeComplete();

            // To accommodate the translation app
            if (gitUIArgs is null)
            {
                return;
            }

            PathToGource = pathToGource;
            GitUIArgs = gitUIArgs;
            GitWorkingDir = gitUIArgs.GitModule.WorkingDir;
            GourceArguments = gourceArguments;

            WorkingDir.Text = GitWorkingDir;
            GourcePath.Text = pathToGource;
            Arguments.Text = GourceArguments;
        }

        private GitUIEventArgs GitUIArgs { get; }

        public string PathToGource { get; set; }

        public string? GitWorkingDir { get; set; }

        public string GourceArguments { get; set; }

        private void RunRealCmdDetached(string cmd, string arguments)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "\"" + cmd + "\"",
                    Arguments = arguments,
                    WorkingDirectory = WorkingDir.Text
                });
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Button1Click(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!File.Exists(GourcePath.Text))
            {
                MessageBox.Show(this, "Cannot find Gource.\nPlease download Gource and set the correct path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            GourceArguments = Arguments.Text;
            string gourceAvatarsDir = GourceArguments.Contains("$(AVATARS)")
                ? ThreadHelper.JoinableTaskFactory.Run(LoadAvatarsAsync)
                : "";
            string arguments = GourceArguments.Replace("$(AVATARS)", gourceAvatarsDir);
            PathToGource = GourcePath.Text;
            GitWorkingDir = WorkingDir.Text;

            RunRealCmdDetached(GourcePath.Text, arguments);
            Close();
        }

        private async Task<string> LoadAvatarsAsync()
        {
            string gourceAvatarsDir = Path.Combine(Path.GetTempPath(), "GitAvatars");

            Directory.CreateDirectory(gourceAvatarsDir);

            foreach (string file in Directory.GetFiles(gourceAvatarsDir))
            {
                File.Delete(file);
            }

            GitArgumentBuilder args = new("log") { "--pretty=format:\"%aE|%aN\"" };
            string[] lines = GitUIArgs.GitModule.GitExecutable.GetOutput(args).Split('\n');

            IEnumerable<(string email, string name)> authors = lines.Select(
                line =>
                {
                    string[] bits = line.Split('|');
                    return (email: bits[0], name: bits[1]);
                })
                .Where(t => !string.IsNullOrWhiteSpace(t.email) && !string.IsNullOrWhiteSpace(t.name))
                .GroupBy(t => t.name)
                .Select(g => (g.First().email, name: g.Key));

            await Task.WhenAll(authors.Select(DownloadImageAsync));

            return gourceAvatarsDir;

            async Task DownloadImageAsync((string email, string name) author)
            {
                try
                {
                    Image image = await AvatarService.DefaultProvider.GetAvatarAsync(author.email, author.name, imageSize: 90);
                    string filename = author.name + ".png";

                    if (image is null || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        return;
                    }

                    string filePath = Path.Combine(gourceAvatarsDir, filename);
                    image.Save(filePath, ImageFormat.Png);
                }
                catch
                {
                    // Do nothing
                }
            }
        }

        private void GourceBrowseClick(object sender, EventArgs e)
        {
            using OpenFileDialog fileDialog =
                new()
                {
                    Filter = "Gource (gource.exe)|gource.exe",
                    FileName = GourcePath.Text
                };
            fileDialog.ShowDialog(this);

            GourcePath.Text = fileDialog.FileName;
        }

        private void WorkingDirBrowseClick(object sender, EventArgs e)
        {
            using FolderBrowserDialog folderDialog = new() { SelectedPath = WorkingDir.Text };
            folderDialog.ShowDialog(this);
            WorkingDir.Text = folderDialog.SelectedPath;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/acaudwell/Gource/");
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/acaudwell/Gource#readme");
        }
    }
}
