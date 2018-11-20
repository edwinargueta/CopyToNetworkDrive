using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Drawing;
using System.ComponentModel;
using ToastNotifications;
using ToastNotifications.Core;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ToastNotifications.Messages;

namespace MapCodeNetwork
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static int NumErrors = 0;
        private System.Windows.Forms.NotifyIcon _notifyIcon;

        Notifier notifier = new Notifier(cfg =>
        {
            cfg.PositionProvider = new PrimaryScreenPositionProvider(Corner.BottomRight, 10, 10);
            cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                notificationLifetime: TimeSpan.FromSeconds(10),
                maximumNotificationCount: MaximumNotificationCount.FromCount(5));
            cfg.Dispatcher = Application.Current.Dispatcher;
        });

        private static void CopyAllFiles(DirectoryInfo currDir, string CodePath)
        {
            bool bDirAttributeChanged = false;
            if (currDir.Attributes.HasFlag(FileAttributes.Hidden))
            {
                currDir.Attributes &= ~FileAttributes.Hidden;
                bDirAttributeChanged = true;
            }

            //Check Subdirectories and use Recurrsion
            DirectoryInfo[] SubLocalDir = currDir.GetDirectories();
            if (SubLocalDir.Length > 0)
            {
                foreach (DirectoryInfo localDir in SubLocalDir)
                {
                    string TempPath = CodePath + "\\" + localDir.Name;
                    if (!Directory.Exists(TempPath))
                    {
                        DirectoryInfo UDirInfo = Directory.CreateDirectory(TempPath);
                        UDirInfo.Attributes = localDir.Attributes;
                        UDirInfo.Attributes &= ~FileAttributes.ReadOnly;
                    }
                    CopyAllFiles(localDir, TempPath);
                }
            }
            
            //Finish copying files on current path
            FileInfo[] LocalFiles = currDir.GetFiles();
            foreach (FileInfo localFile in LocalFiles)
            {
                string TempPath = Path.Combine(CodePath, localFile.Name);
                try
                {
                    bool bFileAttributeChanged = false;
                    

                    if (File.Exists(TempPath))
                    {
                        FileInfo UFile = new FileInfo(TempPath);
                        if (localFile.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            //Delete its hidden value
                            UFile.Attributes &= ~FileAttributes.Hidden;
                            bFileAttributeChanged = true;
                        }

                        //Delete Readonly attribute
                        if (localFile.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            UFile.Attributes &= ~FileAttributes.ReadOnly;
                        }
                    }

                    FileInfo currFile = localFile.CopyTo(TempPath, true);
                    currFile.IsReadOnly = false;

                    //Add same attributes as the local copy
                    string LocalPath = Path.Combine(localFile.DirectoryName, localFile.Name);
                    if (bFileAttributeChanged)
                    {
                        currFile.Attributes = FileAttributes.Hidden;
                    }
                    
                }
                catch (Exception)
                {
                    //log error?
                    NumErrors++;
                    continue;
                }
            }
            if (bDirAttributeChanged)
            {
                currDir.Attributes = FileAttributes.Hidden;
            }
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                //Builds the System Tray notification Icon
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                _notifyIcon.Icon = new Icon("..\\..\\backups.ico");
                _notifyIcon.Visible = true;
                _notifyIcon.Text = "Mapping Code to U: Network Drive";
                _notifyIcon.BalloonTipText = "Mapping Code to U: Network Drive";

                //Declare the File Paths
                //Prefixing path with \\?\ allows bypass of 256 Char limit
                string CodePath = @"\\?\C:\Development";
                //string CodePath = @"\\?\C:\Test";
                string UdrivePath = @"\\?\U:\Development";
                if (Directory.Exists(CodePath))
                {
                    //U Drive is Mapped
                    if (Directory.Exists(UdrivePath))
                    {
                        DirectoryInfo CurrDir = new DirectoryInfo(CodePath);
                        CopyAllFiles(CurrDir, UdrivePath);
                    }
                    //U drive not mapped
                    else
                    {
                        string UDriveConnection = @"use U: \\nwphfs03\" + Environment.UserName + "$";
                        System.Diagnostics.Process.Start("net.exe", UDriveConnection).WaitForExit();
                        if (Directory.Exists(UdrivePath))
                        {
                            DirectoryInfo CurrDir = new DirectoryInfo(CodePath);
                            CopyAllFiles(CurrDir, UdrivePath);
                        }
                        else
                        {
                            //Try Creating Dir
                            Directory.CreateDirectory(UdrivePath);
                            if (Directory.Exists(UdrivePath))
                            {
                                DirectoryInfo CurrDir = new DirectoryInfo(CodePath);
                                CopyAllFiles(CurrDir, UdrivePath);
                            }
                            else
                            {
                                notifier.ShowError("Network Directory not correct!");
                            }
                        }
                    }
                }
                else
                {
                    notifier.ShowError("Local Directory not correct!");
                }

                //Display Message
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += delegate (object s, DoWorkEventArgs args)
                {
                    if (NumErrors > 0)
                    {
                        String WarningMsg = "The number of files not updated: " + NumErrors;
                        notifier.ShowWarning(WarningMsg);
                    }
                    else
                    {
                        String Msg = "All files successfully backed up!";
                        notifier.ShowSuccess(Msg);
                    }
                };
                worker.RunWorkerAsync();

                await Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith((iNum) => { Environment.Exit(0); });

            }
            catch (Exception err)
            {
                MessageBox.Show(err.ToString());
                Environment.Exit(0);
            }      
        }
    }

}
