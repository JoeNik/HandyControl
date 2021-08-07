﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HandyControl.Data;
using HandyControl.Interactivity;

namespace HandyControl.Controls
{
    [TemplatePart(Name = TabHeaderItemsControl, Type = typeof(ItemsControl))]
    [TemplatePart(Name = RootPanel, Type = typeof(Panel))]
    [TemplatePart(Name = ContentPanel, Type = typeof(Panel))]
    public class Ribbon : Selector
    {
        private const string TabHeaderItemsControl = "PART_TabHeaderItemsControl";

        private const string RootPanel = "PART_RootPanel";

        private const string ContentPanel = "PART_ContentPanel";

        private ItemsControl _tabHeaderItemsControl;

        private Panel _rootPanel;

        private Panel _contentPanel;

        private System.Windows.Window _window;

        private double _originHeight;

        private readonly ObservableCollection<object> _tabHeaderItemsSource = new();

        internal ItemsControl RibbonTabHeaderItemsControl => _tabHeaderItemsControl;

        public Ribbon()
        {
            SetRibbon(this, this);

            Loaded += Ribbon_Loaded;
            Unloaded += Ribbon_Unloaded;

            ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
            CommandBindings.Add(new CommandBinding(ControlCommands.Switch, IsMinimizedSwitchButton_OnClick));
        }

        internal static readonly DependencyProperty RibbonProperty = DependencyProperty.RegisterAttached(
            "Ribbon", typeof(Ribbon), typeof(Ribbon),
            new FrameworkPropertyMetadata(default(Ribbon), FrameworkPropertyMetadataOptions.Inherits));

        internal static void SetRibbon(DependencyObject element, Ribbon value)
            => element.SetValue(RibbonProperty, value);

        internal static Ribbon GetRibbon(DependencyObject element)
            => (Ribbon) element.GetValue(RibbonProperty);

        public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
            "IsDropDownOpen", typeof(bool), typeof(Ribbon), new PropertyMetadata(ValueBoxes.TrueBox, OnIsDropDownOpenChanged));

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Ribbon) d).OnIsDropDownOpenChanged((bool) e.NewValue);
        }

        private void OnIsDropDownOpenChanged(bool isDropDownOpen)
        {
            if (_contentPanel == null)
            {
                return;
            }

            var animation = isDropDownOpen ? CreateAnimation(0, ContentHeight) : CreateAnimation(ContentHeight, 0);
            _contentPanel.BeginAnimation(HeightProperty, animation);
        }

        public bool IsDropDownOpen
        {
            get => (bool) GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public static readonly DependencyProperty IsMinimizedProperty = DependencyProperty.Register(
            "IsMinimized", typeof(bool), typeof(Ribbon), new PropertyMetadata(ValueBoxes.FalseBox, OnIsMinimizedChanged));

        private static void OnIsMinimizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Ribbon) d).OnIsMinimizedChanged((bool) e.NewValue);
        }

        private void OnIsMinimizedChanged(bool isMinimized)
        {
            if (_tabHeaderItemsControl == null || _rootPanel == null)
            {
                return;
            }

            var animation = isMinimized
                ? CreateAnimation(_originHeight, _tabHeaderItemsControl.ActualHeight)
                : CreateAnimation(_tabHeaderItemsControl.ActualHeight, _originHeight);
            _rootPanel.BeginAnimation(HeightProperty, animation);
        }

        public bool IsMinimized
        {
            get => (bool) GetValue(IsMinimizedProperty);
            set => SetValue(IsMinimizedProperty, value);
        }

        public static readonly DependencyProperty ContentHeightProperty = DependencyProperty.Register(
            "ContentHeight", typeof(double), typeof(Ribbon), new PropertyMetadata(default(double)));

        public double ContentHeight
        {
            get => (double) GetValue(ContentHeightProperty);
            set => SetValue(ContentHeightProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _tabHeaderItemsControl = GetTemplateChild(TabHeaderItemsControl) as ItemsControl;
            if (_tabHeaderItemsControl is {ItemsSource: null})
            {
                _tabHeaderItemsControl.ItemsSource = _tabHeaderItemsSource;
            }

            _rootPanel = GetTemplateChild(RootPanel) as Panel;
            _contentPanel = GetTemplateChild(ContentPanel) as Panel;

            _originHeight = MeasureOverride(new Size(double.MaxValue, double.MaxValue)).Height;

            if (IsMinimized)
            {
                _rootPanel.SetCurrentValue(HeightProperty, _tabHeaderItemsControl.ActualHeight);
            }

            if (!IsDropDownOpen)
            {
                _contentPanel.SetCurrentValue(HeightProperty, .0);
            }
        }

        internal void ResetSelection()
        {
            SelectedIndex = -1;
            InitializeSelection();
        }

        internal void NotifyMouseClickedOnTabHeader(RibbonTabHeader tabHeader, MouseButtonEventArgs e)
        {
            if (_tabHeaderItemsControl == null)
            {
                return;
            }

            var selectedIndex = _tabHeaderItemsControl.ItemContainerGenerator.IndexFromContainer(tabHeader);

            if (e.ClickCount == 1)
            {
                var currentSelectedIndex = SelectedIndex;

                if (currentSelectedIndex < 0 || currentSelectedIndex != selectedIndex)
                {
                    SelectedIndex = selectedIndex;

                    if (IsMinimized)
                    {
                        IsDropDownOpen = true;
                    }
                }
                else
                {
                    if (IsMinimized)
                    {
                        IsDropDownOpen = !IsDropDownOpen;
                        if (!IsDropDownOpen)
                        {
                            SelectedIndex = -1;
                        }
                    }
                }
            }
            else if (e.ClickCount == 2)
            {
                IsMinimized = !IsMinimized;
                IsDropDownOpen = !IsMinimized;

                if (IsMinimized && !IsDropDownOpen)
                {
                    SelectedIndex = -1;
                }
                else
                {
                    SelectedIndex = selectedIndex;
                }
            }
        }

        internal void NotifyTabHeaderChanged()
        {
            if (ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                return;
            }

            RefreshHeaderCollection();
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            if (e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Replace ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                InitializeSelection();
            }

            if ((ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated ||
                 e.Action != NotifyCollectionChangedAction.Move) && e.Action != NotifyCollectionChangedAction.Remove)
            {
                return;
            }

            RefreshHeaderCollection();
        }

        protected override DependencyObject GetContainerForItemOverride() => new RibbonTab();

        protected override bool IsItemItsOwnContainerOverride(object item) => item is RibbonTab;

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (e.AddedItems is not {Count: > 0})
            {
                return;
            }

            SelectedItem = e.AddedItems[0];
        }

        private void OnPreviewMouseButton(MouseButtonEventArgs e)
        {
            var postion = e.GetPosition(this);
            if (InputHitTest(postion) == null)
            {
                if (IsMinimized && IsDropDownOpen)
                {
                    IsDropDownOpen = false;
                    SelectedIndex = -1;
                }
            }
        }

        private void Ribbon_Loaded(object sender, RoutedEventArgs e)
        {
            _window = System.Windows.Window.GetWindow(this);
            if (_window != null)
            {
                _window.Deactivated += Window_Deactivated;
                _window.PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
                _window.PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
                _window.PreviewMouseRightButtonDown += Window_PreviewMouseRightButtonDown;
                _window.PreviewMouseRightButtonUp += Window_PreviewMouseRightButtonUp;
            }
        }

        private void Ribbon_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_window != null)
            {
                _window.Deactivated -= Window_Deactivated;
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OnPreviewMouseButton(e);

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => OnPreviewMouseButton(e);

        private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => OnPreviewMouseButton(e);

        private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) => OnPreviewMouseButton(e);

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (IsMinimized && IsDropDownOpen)
            {
                IsDropDownOpen = false;
                SelectedIndex = -1;
            }
        }

        private void IsMinimizedSwitchButton_OnClick(object sender, ExecutedRoutedEventArgs e)
        {
            IsDropDownOpen = !IsMinimized;
            if (!IsDropDownOpen)
            {
                SelectedIndex = -1;
            }
        }

        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                return;
            }

            InitializeSelection();
            RefreshHeaderCollection();
        }

        private int GetFirstVisibleTabIndex()
        {
            var count = Items.Count;
            for (var index = 0; index < count; ++index)
            {
                if (ItemContainerGenerator.ContainerFromIndex(index) is RibbonTab {IsVisible: true})
                {
                    return index;
                }
            }

            return -1;
        }

        private void InitializeSelection()
        {
            if (!IsDropDownOpen)
            {
                SelectedIndex = -1;
                return;
            }

            if (SelectedIndex >= 0 || Items.Count <= 0)
            {
                return;
            }

            var firstVisibleTabIndex = GetFirstVisibleTabIndex();
            if (firstVisibleTabIndex < 0)
            {
                return;
            }

            SelectedIndex = firstVisibleTabIndex;
        }

        private void RefreshHeaderCollection()
        {
            var itemsCount = Items.Count;
            for (var index = 0; index < itemsCount; ++index)
            {
                object header = null;
                if (ItemContainerGenerator.ContainerFromIndex(index) is RibbonTab ribbonTab)
                {
                    header = ribbonTab.Header;
                }

                header ??= string.Empty;

                if (index >= _tabHeaderItemsSource.Count)
                {
                    _tabHeaderItemsSource.Add(header);
                }
                else
                {
                    _tabHeaderItemsSource[index] = header;
                }
            }

            var headerCount = _tabHeaderItemsSource.Count;
            for (var index = 0; index < headerCount - itemsCount; ++index)
            {
                _tabHeaderItemsSource.RemoveAt(itemsCount);
            }
        }

        private static DoubleAnimation CreateAnimation(double fromValue, double toValue, double milliseconds = 200)
        {
            return new(fromValue, toValue, new Duration(TimeSpan.FromMilliseconds(milliseconds)))
            {
                EasingFunction = new PowerEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };
        }
    }
}