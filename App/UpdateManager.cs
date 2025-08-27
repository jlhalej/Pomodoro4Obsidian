using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Squirrel;
using Squirrel.Sources;

namespace PomodoroForObsidian
{
    public class UpdateManager
    {
        private readonly string _githubUrl;

        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<int>? UpdateProgress;
        public event EventHandler<UpdateInfo>? UpdateCompleted;

        public UpdateManager(string githubUrl)
        {
            _githubUrl = githubUrl;
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using (var mgr = new Squirrel.UpdateManager(new GithubSource(_githubUrl, "", false)))
                {
                    var updateInfo = await mgr.CheckForUpdate();

                    if (updateInfo.ReleasesToApply.Any())
                    {
                        UpdateAvailable?.Invoke(this, updateInfo);
                        return updateInfo;
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("UpdateManager", $"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DownloadAndApplyUpdatesAsync()
        {
            try
            {
                using (var mgr = new Squirrel.UpdateManager(new GithubSource(_githubUrl, "", false)))
                {
                    var updateInfo = await mgr.CheckForUpdate();
                    if (updateInfo.ReleasesToApply.Any())
                    {
                        Utils.LogDebug("UpdateManager", $"Applying {updateInfo.ReleasesToApply.Count} updates");

                        // Apply updates with progress reporting
                        await mgr.UpdateApp((progress) =>
                        {
                            UpdateProgress?.Invoke(this, progress);
                        });

                        UpdateCompleted?.Invoke(this, updateInfo);

                        // Restart the application to complete the update
                        RestartApplication();
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("UpdateManager", $"Error applying updates: {ex.Message}");
                return false;
            }
        }

        public Version GetCurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version("1.0.0.0");
        }

        public bool IsSquirrelInstalled()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly?.Location == null) return false;

                var assemblyDir = Path.GetDirectoryName(assembly.Location);
                if (assemblyDir == null) return false;

                var updateDotExe = Path.Combine(assemblyDir, "..", "Update.exe");
                return File.Exists(updateDotExe);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets update information without applying updates
        /// </summary>
        public async Task<(bool HasUpdates, string? LatestVersion, string? ReleaseNotes)> GetUpdateInfoAsync()
        {
            try
            {
                using (var mgr = new Squirrel.UpdateManager(new GithubSource(_githubUrl, "", false)))
                {
                    var updateInfo = await mgr.CheckForUpdate();

                    if (updateInfo.ReleasesToApply.Any())
                    {
                        var latest = updateInfo.FutureReleaseEntry;
                        return (true, latest?.Version?.ToString(), "Updates available");
                    }

                    return (false, null, null);
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("UpdateManager", $"Error getting update info: {ex.Message}");
                return (false, null, null);
            }
        }

        /// <summary>
        /// Restarts the application to complete updates
        /// </summary>
        private void RestartApplication()
        {
            try
            {
                // Use Squirrel's restart mechanism if available
                if (IsSquirrelInstalled())
                {
                    var assembly = Assembly.GetEntryAssembly();
                    if (assembly?.Location != null)
                    {
                        var assemblyDir = Path.GetDirectoryName(assembly.Location);
                        var updateExe = Path.Combine(assemblyDir!, "..", "Update.exe");

                        if (File.Exists(updateExe))
                        {
                            // Use Squirrel's Update.exe to restart properly
                            System.Diagnostics.Process.Start(updateExe, $"--processStart \"{Path.GetFileName(assembly.Location)}\"");
                            Application.Current.Shutdown();
                            return;
                        }
                    }
                }

                // Fallback: standard restart
                System.Diagnostics.Process.Start(Environment.ProcessPath ?? Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Utils.LogDebug("UpdateManager", $"Error restarting application: {ex.Message}");
                // If restart fails, at least show a message to the user
                MessageBox.Show("Update completed successfully. Please restart the application manually.",
                              "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
