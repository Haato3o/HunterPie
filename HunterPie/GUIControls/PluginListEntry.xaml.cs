﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HunterPie.UI.Infrastructure;

namespace HunterPie.GUIControls
{
    /// <summary>
    /// Interaction logic for PluginListEntry.xaml
    /// </summary>
    public partial class PluginListEntry : UserControl
    {
        public PluginListEntry()
        {
            InitializeComponent();
            ToggleModeCommand = new ArglessRelayCommand(ToggleMode);
        }

        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            "IsExpanded", typeof(bool), typeof(PluginListEntry), new PropertyMetadata(default(bool)));

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public static readonly DependencyProperty ToggleModeCommandProperty = DependencyProperty.Register(
            "ToggleModeCommand", typeof(ICommand), typeof(PluginListEntry), new PropertyMetadata(default(ICommand)));

        public ICommand ToggleModeCommand
        {
            get { return (ICommand)GetValue(ToggleModeCommandProperty); }
            set { SetValue(ToggleModeCommandProperty, value); }
        }

        private void ToggleMode()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
