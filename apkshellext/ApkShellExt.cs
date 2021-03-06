﻿/***************************************************************************************************************\
 *
 * 
 * Reference : lc_mtt's blog http://blog.csdn.net/lc_mtt
 *             All-In-One Code Framework http://www.codeproject.com/KB/dotnet/CSShellExtContextMenuHand.aspx?q=context+menu+shell+extension+.net
 *             
 * Changelog :
 *             2011-8-25   Base on v2.0
 *                         Remeber last typed in IP, stored in registry
 *                         Select install path, internal memory or SD card.
 *                         Disconnect
 *             2011-8-25   Let system cache the icon
 \**************************************************************************************************************/
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using KKHomeProj.Android;
using KKHomeProj.ShellExtInts;
using Microsoft.Win32;

namespace KKHomeProj.ApkShellExt
{
    [Guid("66391a18-f480-413b-9592-a10044de6cf4"),
    ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class ApkShellExt : IExtractIcon, IPersistFile, IShellExtInit, IContextMenu, IQueryInfo
    {
        #region Constants
        private const string GUID = "{66391a18-f480-413b-9592-a10044de6cf4}";
        private const string KeyName = "apkshellext";
        #endregion

        private uint   MenuConnectWIFI_ID;
        private uint[] MenuInstall2Memory_ID;
        private uint[] MenuInstall2SD_ID;
        private uint[] MenuUninstall_ID;
        private uint[] MenuDisconnect_ID;

        private string sFileName;
        private ArrayList devices;
        private AndroidPackage2 curApk;

        #region IPersistFile 成员

        public void GetClassID(out Guid pClassID)
        {
            pClassID = new Guid(GUID);
            Trace.WriteLine("GetCLSID");
        }

        public void GetCurFile(out string ppszFileName)
        {
            throw new NotImplementedException();
        }

        public int IsDirty()
        {
            throw new NotImplementedException();
        }

        public void Load(string pszFileName, int dwMode)
        {
            sFileName = pszFileName;
            if (curApk == null) curApk = AndroidPackage2.GetAndroidPackage(sFileName);
        }

        public void Save(string pszFileName, bool fRemember)
        {
            throw new NotImplementedException();
        }

        public void SaveCompleted(string pszFileName)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IExtractIcon 成员

        public uint GetIconLocation(ExtractIconOptions uFlags, IntPtr szIconFile, uint cchMax, out int piIndex, out ExtractIconFlags pwFlags)
        {
            piIndex = 0;
            pwFlags = ExtractIconFlags.NotFilename | ExtractIconFlags.PerInstance | ExtractIconFlags.DontCache;
            if (sFileName.Length < cchMax)
            {
                szIconFile = Marshal.StringToHGlobalAuto(sFileName); // return the name for icon cache
            }
            else
            {
                szIconFile = IntPtr.Zero;
                pwFlags = pwFlags | ExtractIconFlags.DontCache;
            }
            NativeMethods.Log("GetIconLocation: pwFlags = " + pwFlags.ToString());
            NativeMethods.Log("GetIconLocation: szIconFile = " + Marshal.PtrToStringAuto(szIconFile));
            NativeMethods.Log("GetIconLocation: piIndex = " + piIndex.ToString());
            NativeMethods.Log("GetIconLocation: ExtractIconOptions.Async = " + uFlags.ToString());
            return ((uFlags & ExtractIconOptions.Async) != 0) ? WinError.E_PENDING : WinError.S_OK;
        }

        public uint Extract(string pszFile, uint nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize)
        {
            NativeMethods.Log("Extract: sFilename = " + sFileName);
            NativeMethods.Log("Extract: pszFile = " + pszFile);
            NativeMethods.Log("Extract: nIconIndex = " + nIconIndex.ToString());
            if (curApk == null) curApk = AndroidPackage2.GetAndroidPackage(sFileName);
            //AndroidPackage.default_icon = Icon.FromHandle(Properties.Resources.deficon.GetHicon());
            int s_size = (int)nIconSize >> 16;
            int l_size = (int)nIconSize & 0xffff;
            phiconLarge = (new Icon(curApk.icon, l_size, l_size)).Handle;
            phiconSmall = (new Icon(curApk.icon, s_size, s_size)).Handle;
            NativeMethods.Log("Extract: Get icon and return");
            return WinError.S_OK;
        }

        #endregion

        #region IShellExtInit Members
        public void Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID)
        {
            if (pDataObj == IntPtr.Zero)
            {
                throw new ArgumentException();
            }

            FORMATETC fe = new FORMATETC();
            fe.cfFormat = (short)CLIPFORMAT.CF_HDROP;
            fe.ptd = IntPtr.Zero;
            fe.dwAspect = DVASPECT.DVASPECT_CONTENT;
            fe.lindex = -1;
            fe.tymed = TYMED.TYMED_HGLOBAL;
            STGMEDIUM stm = new STGMEDIUM();

            // The pDataObj pointer contains the objects being acted upon. In this 
            // example, we get an HDROP handle for enumerating the selected files 
            // and folders.
            IDataObject dataObject = (IDataObject)Marshal.GetObjectForIUnknown(pDataObj);
            dataObject.GetData(ref fe, out stm);

            try
            {
                // Get an HDROP handle.
                IntPtr hDrop = stm.unionmember;
                if (hDrop == IntPtr.Zero)
                {
                    throw new ArgumentException();
                }

                // Determine how many files are involved in this operation.
                uint nFiles = NativeMethods.DragQueryFile(hDrop, UInt32.MaxValue, null, 0);
                // This code sample displays the custom context menu item when only 
                // one file is selected.
                if (nFiles == 1)
                {
                    // Get the path of the file.
                    StringBuilder fileName = new StringBuilder(260);
                    if (0 == NativeMethods.DragQueryFile(hDrop, 0, fileName,
                        fileName.Capacity))
                    {
                        Marshal.ThrowExceptionForHR(WinError.E_FAIL);
                    }
                    sFileName = fileName.ToString();
                }
                else
                {
                    Marshal.ThrowExceptionForHR(WinError.E_FAIL);
                }
            }
            finally
            {
                NativeMethods.ReleaseStgMedium(ref stm);
            }
        }
        #endregion

        #region IContextMenu Members
        public int QueryContextMenu(IntPtr hMenu, uint iMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
        {
            if (((uint)CMF.CMF_DEFAULTONLY & uFlags) != 0)
            {
                return WinError.MAKE_HRESULT(WinError.SEVERITY_SUCCESS, 0, 0);
            }

            devices = AndroidDevice.GetAndroidDevices();

            uint id = 0;
            try
            {
                HMenu submenu = NativeMethods.CreatePopupMenu();
                if (devices.Count > 0)
                {                 
                    MenuInstall2Memory_ID = new uint[devices.Count];
                    MenuInstall2SD_ID = new uint[devices.Count];
                    MenuUninstall_ID = new uint[devices.Count];
                    MenuDisconnect_ID = new uint[devices.Count];

                    for (int i = 0; i<devices.Count; i++)
                    {
                        HMenu subsubmenu = NativeMethods.CreatePopupMenu();
                        MenuInstall2Memory_ID[i] = id;
                        NativeMethods.AppendMenu(subsubmenu, MFMENU.MF_STRING, new IntPtr(idCmdFirst + id++), Properties.Resources.menu_InstallToInternalMemory);
                        MenuInstall2SD_ID[i] = id;
                        NativeMethods.AppendMenu(subsubmenu, MFMENU.MF_STRING, new IntPtr(idCmdFirst + id++), Properties.Resources.menu_InstallToSDCard);
                        MenuUninstall_ID[i] = id;
                        NativeMethods.AppendMenu(subsubmenu, MFMENU.MF_STRING, new IntPtr(idCmdFirst + id++), Properties.Resources.menu_Uninstall);
                        if (((AndroidDevice)devices[i]).ConnectedFromWIFI)
                        {
                            MenuDisconnect_ID[i] = id;
                            NativeMethods.AppendMenu(subsubmenu, MFMENU.MF_STRING, new IntPtr(idCmdFirst + id++), Properties.Resources.menu_DisconnectWIFI);
                        }
                        else
                        {
                            MenuDisconnect_ID[i] = 0;
                        }

                        NativeMethods.InsertMenu(submenu, 1, MFMENU.MF_BYPOSITION | MFMENU.MF_POPUP, subsubmenu.handle, ((AndroidDevice)devices[i]).Serialno);
                        //NativeMethods.AppendMenu(submenu, MFMENU.MF_STRING, new IntPtr(idCmdFirst + id++), s);
                    }
                }
                else
                {
                    NativeMethods.AppendMenu(submenu, MFMENU.MF_STRING | MFMENU.MF_DISABLED | MFMENU.MF_GRAYED, new IntPtr(idCmdFirst + id++), Properties.Resources.menu_CannotFindPhone);
                }
                // a separator
                NativeMethods.AppendMenu(submenu, MFMENU.MF_SEPARATOR, new IntPtr(idCmdFirst + id++), "");
                // Connect with WIFI
                MenuConnectWIFI_ID = id;
                NativeMethods.AppendMenu(submenu, MFMENU.MF_STRING, new IntPtr(idCmdFirst + id++), Properties.Resources.menu_ConnectViaWIFI);
                
                // Insert to popup-menu
                NativeMethods.InsertMenu(new HMenu(hMenu), 1, MFMENU.MF_BYPOSITION | MFMENU.MF_POPUP, submenu.handle, Properties.Resources.menu_InstallToPhone);
            }
            catch {
                return Marshal.GetHRForLastWin32Error();
            }

            // Return an HRESULT value with the severity set to SEVERITY_SUCCESS. 
            // Set the code value to the offset of the largest command identifier 
            // that was assigned, plus one (1).
            return WinError.MAKE_HRESULT(WinError.SEVERITY_SUCCESS, 0, id + 1);
            
        }

        public void InvokeCommand(IntPtr pici)
        {
            //bool isUnicode = false;

            // Determine which structure is being passed in, CMINVOKECOMMANDINFO or 
            // CMINVOKECOMMANDINFOEX based on the cbSize member of lpcmi. Although 
            // the lpcmi parameter is declared in Shlobj.h as a CMINVOKECOMMANDINFO 
            // structure, in practice it often points to a CMINVOKECOMMANDINFOEX 
            // structure. This struct is an extended version of CMINVOKECOMMANDINFO 
            // and has additional members that allow Unicode strings to be passed.
            CMINVOKECOMMANDINFO ici = (CMINVOKECOMMANDINFO)Marshal.PtrToStructure(
                pici, typeof(CMINVOKECOMMANDINFO));
            #region unicode
            //CMINVOKECOMMANDINFOEX iciex = new CMINVOKECOMMANDINFOEX();
            //if (ici.cbSize == Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX)))
            //{
            //    if ((ici.fMask & CMIC.CMIC_MASK_UNICODE) != 0)
            //    {
            //        isUnicode = true;
            //        iciex = (CMINVOKECOMMANDINFOEX)Marshal.PtrToStructure(pici,
            //            typeof(CMINVOKECOMMANDINFOEX));
            //    }
            //}
            #endregion
            // Is the command identifier offset supported by this context menu 
            // extension?
            int id = NativeMethods.LowWord(ici.verb.ToInt32());

            if (id == MenuConnectWIFI_ID)
            {
                string s = "";
                string lastipfile = Path.GetTempPath() + "lastip.txt";
                if (File.Exists(lastipfile))
                {
                    FileStream fs = new FileStream(lastipfile, FileMode.Open);
                    StreamReader sr = new StreamReader(fs);
                    s = sr.ReadLine();
                    sr.Close();
                    fs.Close();
                }
                s = Microsoft.VisualBasic.Interaction.InputBox(Properties.Resources.prompt_ConnectViaWIFI,
                     Properties.Resources.menu_ConnectViaWIFI,
                     s);
                if (!String.IsNullOrEmpty(s))
                {
                    if (NativeMethods.isIPAddress(s))
                    {
                        File.Delete(lastipfile);
                        FileStream fs = new FileStream(lastipfile, FileMode.CreateNew);
                        StreamWriter sw = new StreamWriter(fs);
                        sw.WriteLine(s);
                        sw.Close();
                        fs.Close();

                        (new AndroidToolAdb()).Connect(s);
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show(Properties.Resources.prompt_NotAnIP);
                    }
                }
            }
            else
            {
                for (int i = 0; i < devices.Count; i++) {
                    AndroidDevice d = (AndroidDevice)devices[i];
                    AndroidToolAdb adb = new AndroidToolAdb();
                    if (MenuInstall2Memory_ID[i] == id) {
                        adb.install(d.Serialno, sFileName);
                    } else if (MenuInstall2SD_ID[i] == id) {
                        adb.install(d.Serialno, sFileName, true);
                    } else if (MenuUninstall_ID[i] == id ) {
                        if (curApk == null) curApk = AndroidPackage2.GetAndroidPackage(sFileName);
                        adb.uninstall(d.Serialno, curApk.PackageName);
                    } else if (MenuDisconnect_ID[i] == id ) {
                        adb.Disconnect(d.Serialno);
                    }
                }
            }
        }

        public void GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, System.Text.StringBuilder pszName, uint cchMax)
        {
            pszName.Clear();
            #region not_using_verb
            //if ((GCS)uFlags == GCS.GCS_HELPTEXTW) {
            //    if (idCmd.ToUInt32() < Devices.Count)
            //    {

            //        if (Properties.Resources.menu_comment_InstallToPhone.Length > cchMax - 1)
            //        {
            //            Marshal.ThrowExceptionForHR(WinError.STRSAFE_E_INSUFFICIENT_BUFFER);
            //        }
            //        else
            //        {
            //            pszName.Clear();
            //            pszName.Append(Properties.Resources.menu_comment_InstallToPhone);
            //        }

            //    }
            //    else if (idCmd.ToUInt32() == Devices.Count + 1)
            //    {
            //        if (Properties.Resources.menu_comment_ConnectViaWIFI.Length > cchMax -1){
            //            Marshal.ThrowExceptionForHR(WinError.STRSAFE_E_INSUFFICIENT_BUFFER);
            //        }
            //        else
            //        {
            //            pszName.Clear();
            //            pszName.Append(Properties.Resources.menu_comment_ConnectViaWIFI);
            //        }
            //    }
            //}
            #endregion

        }
        #endregion

        #region IQuaryInfo Members
        public uint GetInfoTip(uint dwFlags, out IntPtr pszInfoTip)
        {
            try
            {
                if (curApk == null) curApk = AndroidPackage2.GetAndroidPackage(sFileName);
                string tip = "Package Name : " + curApk.PackageName +"\nVersion Code : " + curApk.VersionCode +"\nVersion Name : " + curApk.VersionName;
                pszInfoTip = Marshal.StringToCoTaskMemUni(tip);
            }
            catch
            {
                pszInfoTip = IntPtr.Zero;
            }
            return WinError.S_OK;
        }

        public uint GetInfoFlags(out uint dwFlags)
        {
            dwFlags = (uint)QuaryInfoFlags.QITIPF_DEFAULT;
            return WinError.S_OK;
        }
        #endregion

        #region Registeration
        [ComRegisterFunction()]
        public static void Register(Type t)
        {
            try
            {
                //Register APK file type
                RegApk(t.GUID);
                NativeMethods.Log("Register: Registered");
            }
            catch (Exception e)
            {
                NativeMethods.Log("Register: Error happens while register");
                NativeMethods.Log(e.Message);
            }
        }

        [ComUnregisterFunction()]
        public static void Unregister(Type t)
        {
            try
            {
                //unregister file type association
                UnregApk(t.GUID);
                NativeMethods.Log("Unregister: Unregistered");
            }
            catch (Exception e)
            {
                NativeMethods.Log("Unregister: Error happens while unregister");
                NativeMethods.Log(e.Message);
            }
        }

        /// <summary>
        /// register this dll
        /// </summary>
        private static void RegApk(Guid guid)
        {
            RegistryKey root;
            RegistryKey rk;
            root = Registry.LocalMachine;
            try
            {
                rk = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved\",true);
                rk.SetValue(guid.ToString("B"), "Shell Extention for Android Package files");
                rk.Close();
            }
            catch { }
            root.Close();

            root = Registry.ClassesRoot;
            rk = root.CreateSubKey(@".apk");
            rk.SetValue("", "Android Package File");
            rk.Close();
            rk = root.CreateSubKey(@".apk\DefaultIcon");
            rk.SetValue("", @"%1");
            rk.Close();
            rk = root.CreateSubKey(@".apk\shellex\IconHandler");
            rk.SetValue("", guid.ToString("B"));
            rk.Close();
            rk = root.CreateSubKey(@".apk\shellex\ContextMenuHandlers\"+KeyName,RegistryKeyPermissionCheck.ReadWriteSubTree);
            rk.SetValue("", guid.ToString("B"));
            rk.Close();
            rk = root.CreateSubKey(@".apk\shellex\{00021500-0000-0000-C000-000000000046}");
            rk.SetValue("", guid.ToString("B"));
            rk = root.CreateSubKey(@".apk\shellex\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}");
            rk.SetValue("","{c5a40261-cd64-4ccf-84cb-c394da41d590}");
            rk = root.CreateSubKey(@".apk\shellex\{e357fccd-a995-4576-b01f-234630154e96}");
            rk.SetValue("","{c5aec3ec-e812-4677-a9a7-4fee1f9aa000}");
            rk.Close();
            root.Close();

            ////////////////////////////////////////
            //new AndroidToolAapt();
            new AndroidToolAdb();
            NativeMethods.Log("RegApk: before SHChangeNotify");
            NativeMethods.SHChangeNotify(0x08000000, 0,IntPtr.Zero,IntPtr.Zero);// refresh the shell
        }

        /// <summary>
        /// unregister
        /// </summary>
        private static void UnregApk(Guid guid)
        {
            try{
                RegistryKey root;
                RegistryKey rk;
                root = Registry.ClassesRoot;
                root.DeleteSubKeyTree(@".apk\shellex\IconHandler");
                root.DeleteSubKeyTree(@".apk\shellex\ContextMenuHandlers\" + KeyName);
                root.DeleteSubKeyTree(@".apk\shellex\{00021500-0000-0000-C000-000000000046}");
                root.DeleteSubKeyTree(@".apk\shellex\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}");
                root.DeleteSubKeyTree(@".apk\shellex\{e357fccd-a995-4576-b01f-234630154e96}");
                root.Close();

                root = Registry.LocalMachine;
                rk = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved\",true);
                rk.DeleteValue(guid.ToString("B"));
                rk.Close();

                root.Close();
                NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            } catch {}
        }
        #endregion
    }
}
// vim: expandtab tabstop=4 softtabstop=4 shiftwidth=4
