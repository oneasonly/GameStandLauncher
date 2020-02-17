﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Diagnostics;
//using IWshRuntimeLibrary;
using System.IO;
using DisableDevice;
using TsudaKageyu;
using System.Drawing;
using System.Threading;
using ProcessSD = System.Diagnostics.Process;
using Microsoft.Win32;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Logic.Helpers;
using ExceptionManager;

namespace Logic
{
    public static class GameManager
    {

        private static readonly string processName = "GameStand";
        private static readonly string pathShortcuts = @"c:\work\shortcuts";
        private static Type shellObjectType;// = Type.GetTypeFromProgID("WScript.Shell");
        private static dynamic windowsShell;// = Activator.CreateInstance(shellObjectType);
        private const int CountdownTime = 60000;
        public static bool boolTest = false;
        public static readonly List<string> ProcessesAllowed = new List<string>();
        private static CancellationTokenSource ctsPerGame = new CancellationTokenSource();

        static GameManager()
        {
            shellObjectType = Type.GetTypeFromProgID("WScript.Shell");
            windowsShell = Activator.CreateInstance(shellObjectType);
            //SystemManager.OnScreenSaverDetected += ()=>KillAllGames;

            ProcessesAllowed.Add(processName);
            ProcessesAllowed.Add($"devenv"); //Visual Studio
            ProcessesAllowed.Add($"explorer"); //Windows shell
            ProcessesAllowed.Add($"TeamViewer");
        }

        public static async Task RunGame(string _PathGame)
        {
            Ex.Log($"GameManager.RunGame() Нажата кнопка запуска игры");
            Ex.Try(false, () => ctsPerGame.Cancel());
            ctsPerGame = new CancellationTokenSource();
            await RunGame(_PathGame, ctsPerGame.Token);
        }

        private static async Task RunGame(string _PathGame, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(_PathGame))
            { Ex.Throw($"_PathGame({_PathGame}) - is empty at RunGame(string _PathGame)"); }
            if (!File.Exists(_PathGame))
            { Ex.Throw($"File is not exist at '_PathGame({_PathGame})' at RunGame(string _PathGame)"); }

            await KillAllGames();
            //var overlay = new OverlayLauncher();
            var processLaunched = ProcessSD.Start(_PathGame);
            Ex.Log($"GameManager.RunGame(): ProcessLaunched={processLaunched}");
            await Task.Delay(1000);
            var getApps = ProcessSD.GetProcesses().Where(x => !string.IsNullOrEmpty(x.MainWindowTitle) && !isProcessAllowed(x.ProcessName)).ToArray();
            processLaunched = processLaunched ?? getApps.FirstOrDefault();
            Ex.Log($"GameManager.RunGame(): ProcessToFocus={processLaunched}; count={getApps?.Length};");
            SetFokus(processLaunched).RunParallel();
            //Ex.Log($"Process Launched = {processLaunched.MainWindowTitle}({processLaunched.ProcessName});");
            //Ex.Log($"Process Launched={_PathGame}");
            CancellationTokenSource ctsSensor = new CancellationTokenSource();
            Task singleTask = null;
            while (true)
            {
                await Task.Delay(2000);
                if (cancel.IsCancellationRequested)
                {
                    Ex.Try(false, () => ctsSensor.Cancel());
                    break;
                }

                bool isSensorWorks = await DeviceManagerApi.IsSensorExistAsync();
                if (isSensorWorks)
                {
                    Ex.Try(false, () => ctsSensor.Cancel());
                    ctsSensor = new CancellationTokenSource();
                }
                else // NOT isSensorWorks
                {
                    if (singleTask == null || singleTask.IsCompleted)
                    {
                        singleTask = CountDown(ctsSensor.Token);
                    }
                }
            }
            await Task.Yield(); //Just for remove warning
        }

        private static async Task CountDown(CancellationToken cancel)
        {
            Ex.Log("обнаружено: Сенсор отключен.");
            await Task.Delay(CountdownTime, cancel);
            bool isFoundAfterTime = await DeviceManagerApi.IsSensorExistAsync();
            if (!isFoundAfterTime)
            {
                Ex.Log($"Сенсор отключен спустя {CountdownTime/1000}с => Закрываем игры;");
                await KillAllGames();
            }
        }
        private static bool isProcessAllowed(string argUrl) => TrueIfOneContains(argUrl, ProcessesAllowed);
        public static async Task KillAllGames()
        {
            Ex.Log("GameManager.KillAllGames()");
            List<System.Diagnostics.Process> bunchProcesses = System.Diagnostics.Process.GetProcesses().Where(x => x.MainWindowTitle != "")?.ToList();

            await Task.Run(() => Parallel.ForEach(bunchProcesses, item =>
            {
                if (!isProcessAllowed(item.ProcessName))
                {
                    if (!SystemManager.DEBUG)
                    {
                        Ex.Try(() =>
                        {
                            item.Kill();
                            item.WaitForExit();
                        });
                    }
                }
            }));

            System.Diagnostics.Process[] frameHosts = System.Diagnostics.Process.GetProcessesByName("ApplicationFrameHost");
            await Task.Run(() => Parallel.ForEach(frameHosts, item =>
            {
                Ex.Try(() =>
                {
                    item.Kill();
                    item.WaitForExit();
                });
            }));


            await Task.Yield();
        }
        static async Task SetFokus(ProcessSD proc)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    WindowAPI.SetForegroundWindow(proc.MainWindowHandle);                    
                }
                catch{}
                await Task.Delay(1000);
            }            
        }
        public static void KillGame(System.Diagnostics.Process incProcess)
        {
            if (incProcess == null) return;
            Ex.Try( ()=>incProcess.Kill() );
        }
        public static string GetShortcutTarget(string linkPathName)
        {
            if (!File.Exists(linkPathName))
            {
                { Ex.Throw($"File is not exist at 'linkPathName={linkPathName}' at GetShortcutTarget(string linkPathName)"); }
            }
            if (linkPathName.EndsWith(".url"))
            {
                return null;
            }
            return DynamicWshShellExtractor(linkPathName);            
            //return Shell32Extractor(linkPathName);
            //return WshShellExtractor(linkPathName);
        }
        private static string DynamicWshShellExtractor(string linkPathName)
        {
            string shortcutTarget = null;
            if (File.Exists(linkPathName))
            {
                dynamic shortcut;
                Ex.Throw($"Error at DynamicWshShellExtractor(string linkPathName={linkPathName})", () =>
                 {
                     shortcut = windowsShell.CreateShortcut(linkPathName);
                     shortcutTarget = shortcut.TargetPath;
                     shortcut = null;
                 });
            }
            shortcutTarget = string.IsNullOrEmpty(shortcutTarget) ? null : shortcutTarget;
            return shortcutTarget;
        }
        //private static string WshShellExtractor(string linkPathName)
        //{
        //    string shortcutTarget = "";
        //    if (System.IO.File.Exists(linkPathName))
        //    {
        //        WshShell shell = new WshShell();
        //        IWshShortcut link = (IWshShortcut)shell.CreateShortcut(linkPathName);
        //        shortcutTarget = link.TargetPath;
        //    }
        //    if (string.IsNullOrEmpty(shortcutTarget))
        //    { return null; }
        //    return shortcutTarget;
        //}

        //private static string Shell32Extractor(string lnkPath)
        //{
        //    try
        //    {
        //        var shl = new Shell32.Shell();         // Move this to class scope
        //        lnkPath = Path.GetFullPath(lnkPath);
        //        var dir = shl.NameSpace(Path.GetDirectoryName(lnkPath));
        //        var itm = dir.Items().Item(Path.GetFileName(lnkPath));
        //        var lnk = (Shell32.ShellLinkObject)itm.GetLink;
        //        return lnk.Target.Path;
        //    }
        //    catch(Exception ex)
        //    {
        //        ex.Throw();
        //        return null;
        //    }
        //}
        public static string[] GetAllGames()
        {
            string nameDirectory = pathShortcuts;
            if(!Directory.Exists(nameDirectory))
            {
                Ex.Throw($"Error at 'GetAllGames()': can't create directory {nameDirectory}", 
                    ()=>Directory.CreateDirectory(nameDirectory));           
            }
            string imgDirectory = Path.Combine(nameDirectory, "img");
            if (!Directory.Exists(imgDirectory))
            {
                Ex.Throw($"Error at 'GetAllGames()': can't create directory {imgDirectory}",
                    () => Directory.CreateDirectory(imgDirectory));
            }
            return Directory.GetFiles(nameDirectory).Where(s => s.EndsWith(".lnk") || s.EndsWith(".url")).ToArray();
        }
        public static FileInfo[] GetFilesByName(string incFullPath, string dir=null)
        {
            string partialName = new FileInfo(incFullPath).Name;
            partialName = CleanAfterDot(partialName);
            string path = pathShortcuts;
            try
            {
                if (dir != null)
                { path = Path.Combine(path, dir); }
            }
            catch {}
            DirectoryInfo directoryToSearch = new DirectoryInfo(path);
            FileInfo[] filesInDir = new FileInfo[] { };
            try
            {
                filesInDir = directoryToSearch.GetFiles(partialName + ".*");
            }
            catch {}            
            return filesInDir;
        }
        public static ImageSource FindLocalImg(string inc)
        {
            ImageSource imageSource = null;
            var files = GetFilesByName(inc);          
            foreach (var item in files)
            {
                if (item.FullName != inc)
                {
                    try
                    {
                        imageSource = new BitmapImage(new Uri(item.FullName));
                    }
                    catch{}
                    if (imageSource != null) { break; }
                }
            }
            if (imageSource == null)
            {
                files = GameManager.GetFilesByName(inc, "img");
                foreach (var item in files)
                {
                    if (item.FullName != inc)
                    {
                        try
                        {
                            imageSource = new BitmapImage(new Uri(item.FullName));
                        }
                        catch { }
                        if (imageSource != null) { break; }
                    }
                }
            }
            return imageSource;
        }
        public static string CleanAfterDot(string inc)
        {
            string nameClear = null;
            if (inc.Contains('.'))
            {
                var index = inc.LastIndexOf('.');
                nameClear = inc.Substring(0, index);
            }
            return nameClear ?? inc;
        }
        public static Icon GetHighResIcon(string incPath)
        {
            Icon icon = null;
            try
            {
                try
                {
                    IconExtractor ie = new IconExtractor(incPath);
                    icon = ie.GetIcon(0);
                }
                catch (Exception ex)
                {
                    icon = IconFromFilePath(incPath);
                    ex.Log();
                }             
                Icon[] splitIcons = IconUtil.Split(icon); //icon0
                Icon biggestIcon = splitIcons[0];
                foreach (var item in splitIcons)
                {
                    biggestIcon = item.Width > biggestIcon.Width ? item : biggestIcon;
                }
                
                return biggestIcon;
            }
            catch(Exception ex)
            {
                ex.Log($"Error at 'GetHighResIcon(string incPath={incPath})'\n\n{ex.Message}");
                return IconFromFilePath(incPath);
            }            
        }
        private static Icon IconFromFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            { Ex.Throw($"filePath({filePath}) is empty at IconFromFilePath(string filePath)"); }
            if (!File.Exists(filePath))
            { Ex.Throw($"Файл, на который указывает ярлык, не найден {filePath};\n\nGameManager.IconFromFilePath()"); }

            var result = (Icon)null;
            Ex.Throw($"GameManager.IconFromFilePath(): не могу извлечь иконку из {filePath};", 
                ()=> result = Icon.ExtractAssociatedIcon(filePath) );
            return result;
        }
        public static void TestRun(string incPath)
        {
            System.Diagnostics.Process.Start(incPath);
        }
        public static void TestRunOverlay(string incPath)
        {
            var processLaunched = ProcessSD.Start(incPath);
            KillAllGames().RunParallel();
            var overlay = new OverlayLauncher();
            overlay.Start(processLaunched);
        }
        private static bool TrueIfOneContains(string argSource, IList<string> argList)
        {
            foreach (var item in argList)
            {
                if (argSource.ToLower().Contains(item?.ToLower())) return true;
            }
            return false;
        }
    }
}
