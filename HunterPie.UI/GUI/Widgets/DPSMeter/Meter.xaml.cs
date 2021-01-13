﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using HunterPie.Core;
using HunterPie.GUI.Widgets.DPSMeter.Parts;

namespace HunterPie.GUI.Widgets.DPSMeter
{
    /// <summary>
    /// Interaction logic for DPSMeter.xaml
    /// </summary>
    public partial class Meter : Widget
    {
        public new WidgetType Type => WidgetType.DamageWidget;

        private readonly List<PartyMember> players = new List<PartyMember>();

        private Game gContext;

        private Party Context => gContext.Player.PlayerParty;

        public Visibility TimerVisibility
        {
            get => (Visibility)GetValue(TimerVisibilityProperty);
            set => SetValue(TimerVisibilityProperty, value);
        }

        public static readonly DependencyProperty TimerVisibilityProperty =
            DependencyProperty.Register("TimerVisibility", typeof(Visibility), typeof(Meter));

        public Meter(Game ctx)
        {
            InitializeComponent();
            SetWindowFlags();
            SetContext(ctx);
        }

        private void OnMeterRender(object sender, EventArgs e)
        {
            if (Context == null)
                return;

            Timer.Text = Context.Epoch.ToString(@"hh\:mm\:ss\.ff");
        }

        public void SetContext(Game ctx)
        {
            gContext = ctx;
            DamagePlot.SetContext(ctx);
            HookEvents();
        }

        public override void EnterWidgetDesignMode()
        {
            ResizeMode = ResizeMode.CanResize;
            base.EnterWidgetDesignMode();
            RemoveWindowTransparencyFlag();
        }

        public override void LeaveWidgetDesignMode()
        {
            ResizeMode = ResizeMode.NoResize;
            base.LeaveWidgetDesignMode();
            ApplyWindowTransparencyFlag();
        }

        private void HookEvents()
        {
            gContext.Player.OnPeaceZoneEnter += OnPeaceZoneEnter;
            Context.OnTotalDamageChange += OnTotalDamageChange;
            gContext.Player.OnPeaceZoneLeave += OnPeaceZoneLeave;
        }

        public override void SaveSettings()
        {
            UserSettings.PlayerConfig.Overlay.DPSMeter.Position[0] = (int)Left - UserSettings.PlayerConfig.Overlay.Position[0];
            UserSettings.PlayerConfig.Overlay.DPSMeter.Position[1] = (int)Top - UserSettings.PlayerConfig.Overlay.Position[1];
            UserSettings.PlayerConfig.Overlay.DPSMeter.Scale = DefaultScaleX;
            UserSettings.PlayerConfig.Overlay.DPSMeter.Width = Width;
        }

        private void OnPeaceZoneLeave(object source, EventArgs args) =>
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            CompositionTarget.Rendering += OnMeterRender;
            CreatePlayerComponents();
            SortPlayersByDamage();
            if (UserSettings.PlayerConfig.Overlay.DPSMeter.ShowTimerInExpeditions)
            {
                if (Context == null || Context.TotalDamage <= 0) Party.Visibility = Visibility.Collapsed;
                WidgetHasContent = true;
            }
            ChangeVisibility();

            SnapWidget(Width);
        }));

        private void OnPeaceZoneEnter(object source, EventArgs args) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            CompositionTarget.Rendering -= OnMeterRender;
            Timer.Text = "";
            DestroyPlayerComponents();
            ChangeVisibility();
        }));

        public void UnhookEvents()
        {
            CompositionTarget.Rendering -= OnMeterRender;
            gContext.Player.OnPeaceZoneEnter -= OnPeaceZoneEnter;
            gContext.Player.OnPeaceZoneLeave -= OnPeaceZoneLeave;
            Context.OnTotalDamageChange -= OnTotalDamageChange;
            Party.Children.Clear();
            foreach (PartyMember player in players)
            {
                player.UnhookEvents();
            }
            players.Clear();
            DamagePlot.Dispose();
            gContext = null;
        }

        private void OnTotalDamageChange(object source, EventArgs args) =>
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {

                if (Context.TotalDamage > 0 && !UserSettings.PlayerConfig.Overlay.DPSMeter.ShowOnlyTimer)
                {
                    Party.Visibility = Visibility.Visible;
                    WidgetHasContent = true;
                }
                else
                {
                    WidgetHasContent = UserSettings.PlayerConfig.Overlay.DPSMeter.ShowTimerInExpeditions;
                    Party.Visibility = Visibility.Collapsed;
                }
                ChangeVisibility();
                SortPlayersByDamage();
            }));

        private void CreatePlayerComponents()
        {
            for (int i = 0; i < Context.MaxSize; i++)
            {
                PartyMember pMember = new PartyMember(UserSettings.PlayerConfig.Overlay.DPSMeter.PartyMembers[i].Color);
                pMember.SetContext(Context[i], Context);
                players.Add(pMember);
            }
            foreach (PartyMember member in players)
            {
                Party.Children.Add(member);
            }
            if (Context.TotalDamage > 0)
            {
                WidgetHasContent = true;
                ChangeVisibility();
            }
        }

        public void DestroyPlayerComponents()
        {
            foreach (PartyMember player in players)
            {
                player?.UnhookEvents();
            }
            players.Clear();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                WidgetHasContent = false;
                ChangeVisibility();
                Party.Children.Clear();
            }));
        }

        private void SortPlayersByDamage()
        {
            foreach (PartyMember player in players)
            {
                player.UpdateDamage();
            }
            Party.Children.Clear();
            foreach (PartyMember player in players.OrderByDescending(player => player.Context?.Damage))
            {
                Party.Children.Add(player);
            }
        }

        public void UpdatePlayersColor()
        {
            if (players == null || players?.Count <= 0)
                return;

            for (int i = 0; i < Context.MaxSize; i++)
            {
                players[i].ChangeColor(UserSettings.PlayerConfig.Overlay.DPSMeter.PartyMembers[i].Color);
                players[i].UpdateDamageTextSettings();
            }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UnhookEvents();
        }

        public override void ApplySettings()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                Top = UserSettings.PlayerConfig.Overlay.DPSMeter.Position[1] + UserSettings.PlayerConfig.Overlay.Position[1];
                Left = UserSettings.PlayerConfig.Overlay.DPSMeter.Position[0] + UserSettings.PlayerConfig.Overlay.Position[0];
                WidgetActive = UserSettings.PlayerConfig.Overlay.DPSMeter.Enabled;

                TimerVisibility = UserSettings.PlayerConfig.Overlay.DPSMeter.ShowTimer ? Visibility.Visible : Visibility.Collapsed;

                DamagePlot.ApplySettings();
                UpdatePlayersColor();

                ScaleWidget(UserSettings.PlayerConfig.Overlay.DPSMeter.Scale, UserSettings.PlayerConfig.Overlay.DPSMeter.Scale);
                SnapWidget(UserSettings.PlayerConfig.Overlay.DPSMeter.Width);

                Opacity = UserSettings.PlayerConfig.Overlay.DPSMeter.Opacity;
                Color partyBgColor = new Color()
                {
                    R = 0x00,
                    G = 0x00,
                    B = 0x00,
                    A = (byte)(int)(UserSettings.PlayerConfig.Overlay.DPSMeter.BackgroundOpacity * 0xFF)
                };
                SolidColorBrush brush = new SolidColorBrush(partyBgColor);
                brush.Freeze();
                DamageContainer.Background = brush;
                Party.Visibility = UserSettings.PlayerConfig.Overlay.DPSMeter.ShowOnlyTimer ? Visibility.Collapsed : Visibility.Visible;
                if (UserSettings.PlayerConfig.Overlay.DPSMeter.ShowTimerInExpeditions)
                {
                    if (Context == null || Context.TotalDamage <= 0)
                        Party.Visibility = Visibility.Collapsed;
                }
                WidgetHasContent = true;
                if (Context != null)
                {
                    WidgetHasContent = (UserSettings.PlayerConfig.Overlay.DPSMeter.ShowTimerInExpeditions || Context?.TotalDamage > 0) && !gContext.Player.InPeaceZone;
                    SortPlayersByDamage();
                }
                base.ApplySettings();
            }));
        }
            

        public override void ScaleWidget(double newScaleX, double newScaleY)
        {
            if (newScaleX <= 0.2)
                return;

            DamageContainer.LayoutTransform = new ScaleTransform(newScaleX, newScaleY);
            DefaultScaleX = newScaleX;
            DefaultScaleY = newScaleY;
            SnapWidget(UserSettings.PlayerConfig.Overlay.DPSMeter.Width);
        }

        private void DamageMeter_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            e.Handled = true;
            SnapWidget(e.NewSize.Width);
        }

        private void SnapWidget(double newWidth)
        {
            double temp;
            const int defaultWidth = 322;
            const int memberItemHeight = 46;
            const int plotHeight = 100;

            if (newWidth / (defaultWidth * DefaultScaleX) % 1 >= 0.5)
            {
                temp = (defaultWidth * DefaultScaleX) * Math.Ceiling(newWidth / (defaultWidth * DefaultScaleX));
            } else
            {
                temp = (defaultWidth * DefaultScaleX) * Math.Floor(newWidth / (defaultWidth * DefaultScaleX));
            }
            if (newWidth < (defaultWidth * DefaultScaleX))
            {
                temp = (defaultWidth * DefaultScaleX);
            }
            else if (newWidth > (defaultWidth * DefaultScaleX) * 4) temp = (defaultWidth * DefaultScaleX) * 4;
            Width = temp;
            MaxHeight = MinHeight = (
                TimerContainer.ActualHeight
                + (Party.ActualHeight == 0 ? memberItemHeight * 4 : Party.ActualHeight)
                + 2
                + (UserSettings.PlayerConfig.Overlay.DPSMeter.EnableDamagePlot ? plotHeight : 0)
            ) * DefaultScaleY;
        }
    }
}
