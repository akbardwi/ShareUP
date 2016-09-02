﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2016 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ShareX.Setup
{
    internal class Program
    {
        [Flags]
        private enum SetupJobs
        {
            None = 0,
            CreateSetup = 1,
            CreatePortable = 1 << 1,
            CreateSteamFolder = 1 << 2,
            CreatePortableAppsFolder = 1 << 3,
            OpenOutputDirectory = 1 << 4,
            UploadOutputFile = 1 << 5,

            Stable = CreateSetup | CreatePortable | OpenOutputDirectory,
            Setup = CreateSetup | OpenOutputDirectory,
            Portable = CreatePortable | OpenOutputDirectory,
            Steam = CreateSteamFolder | OpenOutputDirectory,
            PortableApps = CreatePortableAppsFolder | OpenOutputDirectory,
            Beta = CreateSetup | UploadOutputFile,
            AppVeyorRelease = CreateSetup | CreatePortable,
            AppVeyorSteam = CreateSteamFolder
        }

        private static SetupJobs Job = SetupJobs.None;
        private static bool AppVeyor = false;

        private static string ParentDir => AppVeyor ? "" : @"..\..\..\";
        private static string BinDir => Path.Combine(ParentDir, "ShareUP", "bin");
        private static string ReleaseDir => Path.Combine(BinDir, "Release");
        private static string DebugDir => Path.Combine(BinDir, "Debug");
        private static string DebugExecutablePath => Path.Combine(DebugDir, "ShareUP.exe");
        private static string SteamDir => Path.Combine(BinDir, "Steam");

        private static string InnoSetupDir => Path.Combine(ParentDir, @"ShareUP.Setup\InnoSetup");
        private static string OutputDir => Path.Combine(InnoSetupDir, "Output");
        private static string PortableOutputDir => Path.Combine(OutputDir, "ShareUP-portable");
        private static string SteamOutputDir => Path.Combine(OutputDir, "ShareUP-Steam");
        private static string PortableAppsOutputDir => Path.Combine(ParentDir, @"..\PortableApps\ShareXPortable\App\ShareUP");

        private static string SteamLauncherDir => Path.Combine(ParentDir, @"ShareUP.Steam\bin\Release");
        private static string SteamUpdatesDir => Path.Combine(SteamOutputDir, "Updates");
        private static string ChromeDir => Path.Combine(ParentDir, @"ShareUP.Chrome\bin\Release");
        private static string RecorderDevicesSetupPath => Path.Combine(OutputDir, "Recorder-devices-setup.exe");

        public static string InnoSetupCompilerPath = @"C:\Program Files (x86)\Inno Setup 5\ISCC.exe";
        public static string ZipPath = @"C:\Program Files\7-Zip\7z.exe";
        public static string FFmpeg32bit => Path.Combine(ParentDir, "Lib", "ffmpeg.exe");
        public static string FFmpeg64bit => Path.Combine(ParentDir, "Lib", "ffmpeg-x64.exe");

        private static void Main(string[] args)
        {
            Console.WriteLine("ShareUP setup started.");

            if (Helpers.CheckArguments(args, "-AppVeyorRelease"))
            {
                AppVeyor = true;
                Job = SetupJobs.AppVeyorRelease;
            }
            else if (Helpers.CheckArguments(args, "-AppVeyorSteam"))
            {
                AppVeyor = true;
                Job = SetupJobs.AppVeyorSteam;
            }

            Console.WriteLine("Setup job: " + Job);

            if (Directory.Exists(OutputDir))
            {
                Console.WriteLine("Cleaning output directory: " + OutputDir);

                Directory.Delete(OutputDir, true);
            }

            if (Job.HasFlag(SetupJobs.CreateSetup))
            {
                CompileSetup();
            }

            if (Job.HasFlag(SetupJobs.CreatePortable))
            {
                CreatePortable(PortableOutputDir, ReleaseDir);
            }

            if (Job.HasFlag(SetupJobs.CreateSteamFolder))
            {
                CreateSteamFolder();
            }

            if (Job.HasFlag(SetupJobs.CreatePortableAppsFolder))
            {
                CreatePortable(PortableAppsOutputDir, ReleaseDir);
            }

            if (Job.HasFlag(SetupJobs.OpenOutputDirectory))
            {
                OpenOutputDirectory();
            }

            if (Job.HasFlag(SetupJobs.UploadOutputFile))
            {
                UploadLatestFile();
            }

            Console.WriteLine("ShareUP setup successfully completed.");
        }

        private static void CompileSetup()
        {
            CompileISSFile("Recorder-devices-setup.iss");
            CompileISSFile("ShareUP-setup.iss");
        }

        private static void InstallInnoSetup()
        {
            string innoSetupFilename = Helpers.DownloadFile("http://files.jrsoftware.org/is/5/innosetup-5.5.9-unicode.exe");

            Console.WriteLine("Installing InnoSetup.");

            Process.Start(innoSetupFilename, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-").WaitForExit();

            Console.WriteLine("InnoSetup installed.");
        }

        private static void CompileISSFile(string filename)
        {
            if (AppVeyor && !File.Exists(InnoSetupCompilerPath))
            {
                InstallInnoSetup();
            }

            if (File.Exists(InnoSetupCompilerPath))
            {
                Console.WriteLine("Compiling setup file: " + filename);

                ProcessStartInfo startInfo = new ProcessStartInfo(InnoSetupCompilerPath, $"\"{filename}\"");
                startInfo.UseShellExecute = false;
                startInfo.WorkingDirectory = Path.GetFullPath(InnoSetupDir);
                Process process = Process.Start(startInfo);
                process.WaitForExit();

                Console.WriteLine("Setup file is created.");
            }
            else
            {
                Console.WriteLine("InnoSetup compiler is missing: " + InnoSetupCompilerPath);
            }
        }

        private static void CreateSteamFolder()
        {
            Console.WriteLine("Creating Steam folder:" + SteamOutputDir);

            if (Directory.Exists(SteamOutputDir))
            {
                Directory.Delete(SteamOutputDir, true);
            }

            Directory.CreateDirectory(SteamOutputDir);

            Helpers.CopyFile(Path.Combine(SteamLauncherDir, "ShareUP_Launcher.exe"), SteamOutputDir);
            Helpers.CopyFile(Path.Combine(SteamLauncherDir, "steam_appid.txt"), SteamOutputDir);
            Helpers.CopyFile(Path.Combine(SteamLauncherDir, "installscript.vdf"), SteamOutputDir);
            Helpers.CopyFiles(SteamLauncherDir, "*.dll", SteamOutputDir);

            CreatePortable(SteamUpdatesDir, SteamDir);
        }

        private static void CreatePortable(string destination, string releaseDirectory)
        {
            Console.WriteLine("Creating portable: " + destination);

            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            Directory.CreateDirectory(destination);

            Helpers.CopyFile(Path.Combine(releaseDirectory, "ShareUP.exe"), destination);
            Helpers.CopyFile(Path.Combine(releaseDirectory, "ShareUP.exe.config"), destination);
            Helpers.CopyFiles(releaseDirectory, "*.dll", destination);
            Helpers.CopyFiles(Path.Combine(ParentDir, "Licenses"), "*.txt", Path.Combine(destination, "Licenses"));

            if (!File.Exists(RecorderDevicesSetupPath))
            {
                CompileISSFile("Recorder-devices-setup.iss");
            }

            Helpers.CopyFile(RecorderDevicesSetupPath, destination);
            Helpers.CopyFile(Path.Combine(ChromeDir, "ShareUP_Chrome.exe"), destination);

            string[] languages = new string[] { "de", "es", "fr", "hu", "ko-KR", "nl-NL", "pt-BR", "ru", "tr", "vi-VN", "zh-CN" };

            foreach (string language in languages)
            {
                Helpers.CopyFiles(Path.Combine(releaseDirectory, language), "*.resources.dll", Path.Combine(destination, "Languages", language));
            }

            if (destination.Equals(SteamUpdatesDir, StringComparison.InvariantCultureIgnoreCase))
            {
                CopyFFmpeg(destination);
            }
            else if (destination.Equals(PortableAppsOutputDir, StringComparison.InvariantCultureIgnoreCase))
            {
                File.Create(Path.Combine(destination, "PortableApps")).Dispose();
            }
            else
            {
                File.Create(Path.Combine(destination, "Portable")).Dispose();

                //FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Path.Combine(releaseDir, "ShareX.exe"));
                //string zipFilename = string.Format("ShareX-{0}.{1}.{2}-portable.zip", versionInfo.ProductMajorPart, versionInfo.ProductMinorPart, versionInfo.ProductBuildPart);
                string zipPath = Path.Combine(OutputDir, "ShareUP-portable.zip");

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                Helpers.Zip(destination + "\\*", zipPath);

                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, true);
                }
            }

            Console.WriteLine("Portable created.");
        }

        private static void CopyFFmpeg(string destination)
        {
            if (!File.Exists(FFmpeg32bit))
            {
                string filename = Helpers.DownloadFile("https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-20160827-c752733-win32-static.zip");
                Helpers.Unzip(filename, "ffmpeg.exe");
                File.Move("ffmpeg.exe", FFmpeg32bit);
            }

            Helpers.CopyFile(FFmpeg32bit, destination);

            if (!File.Exists(FFmpeg64bit))
            {
                string filename = Helpers.DownloadFile("https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-20160827-c752733-win64-static.zip");
                Helpers.Unzip(filename, "ffmpeg.exe");
                File.Move("ffmpeg.exe", FFmpeg64bit);
            }

            Helpers.CopyFile(FFmpeg64bit, destination);
        }

        private static void OpenOutputDirectory()
        {
            Process.Start(OutputDir);
        }

        private static void UploadLatestFile()
        {
            FileInfo fileInfo = new DirectoryInfo(OutputDir).GetFiles("*.exe").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();

            if (fileInfo != null)
            {
                Console.WriteLine("Uploading setup file.");
                Process.Start(DebugExecutablePath, fileInfo.FullName);
            }
        }
    }
}