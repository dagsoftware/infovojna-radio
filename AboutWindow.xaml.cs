/*
 * Copyright(C) 2019 DAG Software
 * based on Tray Radio 1.5.2 by Michal Heczko 2017
 * All rights reserved.
 *
 * This software may be modified and distributed under the terms
 * of the BSD license.  See the LICENSE file for details.
 */

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace InfovojnaRadio
{
	/// <summary>
	/// Interaction logic for AboutWindow.xaml
	/// </summary>
	public partial class AboutWindow : Window
	{
		#region Fields

		public static readonly RoutedCommand CommandShowLicense = new RoutedCommand();

		#endregion

		#region Properties

		public static string Version
		{
			get
			{
				string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
				return version.Substring(0, version.LastIndexOf('.'));
			}
		}

		#endregion

		#region Methods

		private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
		{
			this.Close();
		}

		#endregion


		#region Constructor

		public AboutWindow()
		{
			InitializeComponent();
			
			lblNameAndVersion.Content = string.Format("Infovojna-radio {0}", Version);
		}

		#endregion
	}
}
