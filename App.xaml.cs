/*
 * Copyright(C) 2019 DAG Software
 * based on Tray Radio 1.5.2 by Michal Heczko 2017
 * All rights reserved.
 *
 * This software may be modified and distributed under the terms
 * of the BSD license.  See the LICENSE file for details.
 */

using Microsoft.Win32;
using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Un4seen.Bass;

namespace InfovojnaRadio
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{
		#region Fields

		private static readonly Icon IconAntennaSignal = Icon.FromHandle(InfovojnaRadio.Properties.Resources.Antenna_Signal.GetHicon());
		private static readonly Icon IconAntennaSignalStalled = Icon.FromHandle(InfovojnaRadio.Properties.Resources.Antenna_Signal_Stalled.GetHicon());
		private static readonly Icon IconAntennaNoSignal = Icon.FromHandle(InfovojnaRadio.Properties.Resources.Antenna_No_Signal.GetHicon());

		private BalanceVolumeWindow _balanceVolumeWnd;
		private CancellationTokenSource _cts = new CancellationTokenSource();
		private bool _isInitialised;
		private Mutex _mutex = null;
		private PreferencesWindow _preferencesWnd;
		private BASSActive _radioStatus = BASSActive.BASS_ACTIVE_STOPPED;
		private NotifyIcon _trayIcon;

		#endregion

		#region Properties

		public RadioEntry ActiveRadio { get; set; }

		public static App Instance { get; private set; }
		
		#endregion

		#region Methods

		protected void CreateRadioMenu()
		{
			_trayIcon.ContextMenu = new ContextMenu();
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("Zastaviť prehrávanie", (object sender, EventArgs args) => { ActiveRadio?.Stop(); }));
			_trayIcon.ContextMenu.MenuItems[0].Enabled = false;
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("-"));
			foreach (RadioEntry radio in InfovojnaRadio.Properties.Settings.Default.Radios)
			{
				radio.OnBeforePlay += RadioBeforePlay;
				radio.OnAfterPlay += RadioAfterPlay;
				_trayIcon.ContextMenu.MenuItems.Add(radio.MenuItem);
			}
			if (InfovojnaRadio.Properties.Settings.Default.Radios.Count > 0)
				_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("-"));
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("-"));
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("Nastavenia", (object sender, EventArgs args) =>
			{
				if (_preferencesWnd == null)
				{
					_preferencesWnd = new PreferencesWindow();
					_preferencesWnd.Closed += (object sender2, EventArgs args2) => { _preferencesWnd = null; };
					_preferencesWnd.ShowDialog();
				}
				else
					_preferencesWnd.Focus();
			}));
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("-"));
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("O programe", (object sender, EventArgs args) =>
			{
				(new AboutWindow()).ShowDialog();
			}));
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("-"));
			_trayIcon.ContextMenu.MenuItems.Add(new MenuItem("Ukončiť", (object sender, EventArgs args) => { Shutdown(0); }));
		}

		protected override void OnExit(ExitEventArgs e)
		{
			if (_isInitialised)
			{
				ActiveRadio?.Stop();
				Bass.BASS_Free();
				_cts.Cancel();
				_trayIcon.Dispose();
				_mutex.ReleaseMutex();
			}
			InfovojnaRadio.Properties.Settings.Default.Save();
			base.OnExit(e);
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			_mutex = new Mutex(false, Current.ToString());
			if (_mutex.WaitOne(500, false))
			{
				if ((_isInitialised = Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero)))
				{
					_trayIcon = new NotifyIcon();
					_trayIcon.MouseClick += (object sender, MouseEventArgs args) =>
					{
						if (args.Button == MouseButtons.Left)
						{
							if (_balanceVolumeWnd == null)
							{
								_balanceVolumeWnd = new BalanceVolumeWindow();
								_balanceVolumeWnd.Closed += (object sender2, EventArgs args2) => { _balanceVolumeWnd = null; };
								_balanceVolumeWnd.Show();
							}
							else if (_balanceVolumeWnd.IsVisible)
								_balanceVolumeWnd.Hide();
							else
								_balanceVolumeWnd.Show();
						}
					};
					_trayIcon.Icon = IconAntennaNoSignal;
					_trayIcon.Text = InfovojnaRadio.Properties.Resources.InfovojnaRadio;
					_trayIcon.Visible = true;
					CreateRadioMenu();
					Task.Factory.StartNew((Action)(() =>
					{
						do
						{
							if (ActiveRadio != null)
							{
								BASSActive status = ActiveRadio.Status;
								switch (status)
								{
									case BASSActive.BASS_ACTIVE_PLAYING:
										_trayIcon.ContextMenu.MenuItems[0].Enabled = true;
										_trayIcon.Icon = IconAntennaSignal;
										string text = string.Format("{0} - {1}", (object)InfovojnaRadio.Properties.Resources.InfovojnaRadio, ActiveRadio.Name);
										_trayIcon.Text = text.Length >= 60 ? text.Substring(0, 60) + "..." : text;
										if (ActiveRadio.IsNewSong || (status != _radioStatus))
										{
											ShowBallonTip(string.Format("{0} - Prehráva sa\n\n{1}", ActiveRadio.Name, ActiveRadio.Info.Title), ToolTipIcon.Info);
										}
										break;
									case BASSActive.BASS_ACTIVE_STALLED:
										_trayIcon.Icon = IconAntennaSignalStalled;
										if (status != _radioStatus)
											ShowBallonTip(string.Format("{0} - Zastavené", ActiveRadio.Name), ToolTipIcon.Warning);//stalled
										break;
									default:
										_trayIcon.ContextMenu.MenuItems[0].Enabled = false;
										_trayIcon.Icon = IconAntennaNoSignal;
										_trayIcon.Text = InfovojnaRadio.Properties.Resources.InfovojnaRadio;
										if (status != _radioStatus)
											ShowBallonTip(string.Format("{0} - Zastavené", ActiveRadio.Name), ToolTipIcon.Info);//stopped
										break; 
								}
								_radioStatus = status;
							}
							Thread.Sleep(1);
						} while (!_cts.IsCancellationRequested);
					}));
					// Autoplay...
					if (InfovojnaRadio.Properties.Settings.Default.AutoplayRadio)
						(ActiveRadio = InfovojnaRadio.Properties.Settings.Default.Radios[InfovojnaRadio.Properties.Settings.Default.AutoplayRadioName]).Play();
					base.OnStartup(e);
				}
				else
					Shutdown(-1);
			}
			else
			{
				System.Windows.MessageBox.Show("Program Infovojna-radio je už spustený.", InfovojnaRadio.Properties.Resources.InfovojnaRadio, MessageBoxButton.OK, MessageBoxImage.Warning);
				Shutdown(0);
			}
		}

		private void RadioAfterPlay(object sender, EventArgs args)
		{
			ActiveRadio = sender as RadioEntry;
			ActiveRadio.Balance = InfovojnaRadio.Properties.Settings.Default.Balance;
			ActiveRadio.Volume = InfovojnaRadio.Properties.Settings.Default.Volume;
			ActiveRadio.Mute = InfovojnaRadio.Properties.Settings.Default.Mute;
		}

		private void RadioBeforePlay(object sender, EventArgs args)
		{
			ActiveRadio?.Stop();
		}

		public void ShowBallonTip(string message, ToolTipIcon icon = ToolTipIcon.Error)
		{
			_trayIcon.BalloonTipIcon = icon;
			_trayIcon.BalloonTipText = message;
			_trayIcon.BalloonTipTitle = InfovojnaRadio.Properties.Resources.InfovojnaRadio;
			_trayIcon.ShowBalloonTip(100);
		}
		
		#endregion

		#region Constructor

		public App()
		{
			Instance = this;
			if (InfovojnaRadio.Properties.Settings.Default.Radios == null)
			{
				InfovojnaRadio.Properties.Settings.Default.Radios = new RadioCollection();
				InfovojnaRadio.Properties.Settings.Default.Radios.Add(new RadioEntry("InfoVojna", "http://stream.infovojna.sk:8000/live128"));
			}
			InfovojnaRadio.Properties.Settings.Default.Radios.CollectionChanged += (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
				{
					switch (e.Action)
					{
						case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
							foreach (RadioEntry item in e.NewItems)
								_trayIcon.ContextMenu.MenuItems.Add(InfovojnaRadio.Properties.Settings.Default.Radios.Count, item.MenuItem);
							if (_trayIcon.ContextMenu.MenuItems[1].Text.CompareTo("-") != 0)
								_trayIcon.ContextMenu.MenuItems.Add(1, new MenuItem("-"));
							break;
						case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
							RadioEntry radio = e.OldItems[0] as RadioEntry;
							_trayIcon.ContextMenu.MenuItems.Remove(radio.MenuItem);
							if (ActiveRadio?.Name.CompareTo(radio.Name) == 0)
								if (ActiveRadio.IsActive)
								{
									ActiveRadio.Stop();
									ActiveRadio = null;
								}
							if (InfovojnaRadio.Properties.Settings.Default.Radios.Count == 0)
								_trayIcon.ContextMenu.MenuItems.RemoveAt(1);
							break;
					}
				};
			InfovojnaRadio.Properties.Settings.Default.PropertyChanged += (object sender, System.ComponentModel.PropertyChangedEventArgs e) =>
			{
				switch (e.PropertyName)
				{
					case "Autostart":
						Uri uriAppFile = new Uri(Assembly.GetExecutingAssembly().CodeBase);
						RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\", true);
						if (InfovojnaRadio.Properties.Settings.Default.Autostart)
							runKey.SetValue("Infovojna-radio", string.Format("\"{0}\"", uriAppFile.LocalPath), RegistryValueKind.String);
						else
							runKey.DeleteValue("Infovojna-radio", false);
						runKey.Flush();
						runKey.Close();
						break;
					case "Balance":
						if (ActiveRadio != null)
							ActiveRadio.Balance = InfovojnaRadio.Properties.Settings.Default.Balance;
						break;
					case "Mute":
						if (ActiveRadio != null)
							ActiveRadio.Mute = InfovojnaRadio.Properties.Settings.Default.Mute;
						break;
					case "Volume":
						if (ActiveRadio != null)
							ActiveRadio.Volume = InfovojnaRadio.Properties.Settings.Default.Volume;
						break;
				}
			};
		}
		
		#endregion
	}
}
