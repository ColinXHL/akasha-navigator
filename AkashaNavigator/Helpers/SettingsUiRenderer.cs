using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// 设置 UI 渲染器
/// 根据 SettingsUiDefinition 动态生成 WPF 控件
/// </summary>
public class SettingsUiRenderer
{
#region Events

    /// <summary>
    /// 配置值变更事件
    /// </summary>
    public event EventHandler<SettingsValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// 按钮动作事件
    /// </summary>
    public event EventHandler<SettingsButtonActionEventArgs>? ButtonAction;

#endregion

#region Fields

    private readonly PluginConfig _config;
    private readonly SettingsUiDefinition _definition;
    private readonly Dictionary<string, FrameworkElement> _controlMap = new();
    private readonly Dictionary<string, SettingsItem> _itemMap = new();

#endregion

#region Constructor

    /// <summary>
    /// 创建设置 UI 渲染器
    /// </summary>
    /// <param name="definition">设置 UI 定义</param>
    /// <param name="config">插件配置</param>
    public SettingsUiRenderer(SettingsUiDefinition definition, PluginConfig config)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

#endregion

#region Public Methods

    /// <summary>
    /// 渲染设置 UI
    /// </summary>
    /// <returns>包含所有设置控件的面板</returns>
    public FrameworkElement Render()
    {
        var panel = new StackPanel();

        if (_definition.Sections == null || _definition.Sections.Count == 0)
            return panel;

        foreach (var section in _definition.Sections)
        {
            RenderSection(panel, section);
        }

        return panel;
    }

    /// <summary>
    /// 获取指定键的控件
    /// </summary>
    public FrameworkElement? GetControl(string key)
    {
        return _controlMap.TryGetValue(key, out var control) ? control : null;
    }

    /// <summary>
    /// 刷新所有控件的值
    /// </summary>
    public void RefreshValues()
    {
        foreach (var kvp in _controlMap)
        {
            // 获取对应的 SettingsItem 以便使用默认值
            _itemMap.TryGetValue(kvp.Key, out var item);
            RefreshControlValue(kvp.Key, kvp.Value, item);
        }
    }

#endregion

#region Section Rendering

    private void RenderSection(StackPanel parent, SettingsSection section)
    {
        // 分组标题
        if (!string.IsNullOrWhiteSpace(section.Title))
        {
            var header = new TextBlock { Text = section.Title };

            // 尝试应用共享样式，如果不存在则使用回退样式
            if (!ApplyStyleIfExists(header, "SettingsGroupHeaderStyle"))
            {
                header.FontSize = 13;
                header.FontWeight = FontWeights.SemiBold;
                header.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                header.Margin = new Thickness(0, 16, 0, 8);
            }

            parent.Children.Add(header);
        }

        // 渲染分组内的设置项
        if (section.Items != null)
        {
            foreach (var item in section.Items)
            {
                var control = RenderItem(item);
                if (control != null)
                {
                    parent.Children.Add(control);
                }
            }
        }
    }

#endregion

#region Item Rendering

    private FrameworkElement? RenderItem(SettingsItem item)
    {
        return item.Type.ToLowerInvariant() switch { "text" => RenderTextBox(item),
                                                     "number" => RenderNumberBox(item),
                                                     "checkbox" => RenderCheckBox(item),
                                                     "select" => RenderComboBox(item),
                                                     "slider" => RenderSlider(item),
                                                     "button" => RenderButton(item),
                                                     "group" => RenderGroupBox(item),
                                                     "processlist" => RenderProcessList(item),
                                                     _ => null };
    }

#endregion

#region TextBox Rendering

    private FrameworkElement RenderTextBox(SettingsItem item)
    {
        var container = CreateItemContainer(item.Label);

        var textBox = new TextBox { MinWidth = 200 };

        // 尝试应用共享样式，如果不存在则使用回退样式
        if (!ApplyStyleIfExists(textBox, "DarkTextBoxStyle"))
        {
            textBox.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            textBox.Foreground = Brushes.White;
            textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            textBox.BorderThickness = new Thickness(1);
            textBox.Padding = new Thickness(8, 6, 8, 6);
            textBox.FontSize = 12;
            ApplyRoundedTextBoxStyle(textBox);
        }

        // 设置占位符 (DarkTextBoxStyle 使用 Tag 作为 placeholder)
        if (!string.IsNullOrEmpty(item.Placeholder))
        {
            textBox.Tag = item.Placeholder;
        }

        // 加载当前值或默认值
        var currentValue = GetConfigValue<string>(item.Key, item.GetDefaultValue<string>() ?? string.Empty);
        textBox.Text = currentValue;

        // 值变更事件
        textBox.TextChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(item.Key))
            {
                OnValueChanged(item.Key, textBox.Text);
            }
        };

        if (!string.IsNullOrEmpty(item.Key))
        {
            _controlMap[item.Key] = textBox;
            _itemMap[item.Key] = item;
        }

        container.Children.Add(textBox);
        return container;
    }

    /// <summary>
    /// 应用圆角 TextBox 样式
    /// </summary>
    private void ApplyRoundedTextBoxStyle(TextBox textBox)
    {
        var template = new ControlTemplate(typeof(TextBox));

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollViewerFactory.Name = "PART_ContentHost";
        scrollViewerFactory.SetValue(ScrollViewer.MarginProperty, new System.Windows.Data.Binding("Padding") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        borderFactory.AppendChild(scrollViewerFactory);
        template.VisualTree = borderFactory;

        // 触发器
        var focusTrigger = new Trigger { Property = UIElement.IsFocusedProperty, Value = true };
        focusTrigger.Setters.Add(
            new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "border"));
        focusTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)), "border"));
        template.Triggers.Add(focusTrigger);

        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(
            new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), "border"));
        template.Triggers.Add(mouseOverTrigger);

        textBox.Template = template;
    }

#endregion

#region NumberBox Rendering

    private FrameworkElement RenderNumberBox(SettingsItem item)
    {
        var container = CreateItemContainer(item.Label);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                                    Foreground = Brushes.White,
                                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                                    BorderThickness = new Thickness(1),
                                    Padding = new Thickness(8, 0, 8, 0),
                                    FontSize = 12,
                                    MinWidth = 100,
                                    Height = 32,
                                    VerticalContentAlignment = VerticalAlignment.Center };

        // 应用圆角样式
        ApplyRoundedTextBoxStyle(textBox);

        // 加载当前值或默认值（强制取整）
        var defaultValue = (int)(item.GetDefaultValue<double?>() ?? 0);
        var currentValue = (int)GetConfigValue(item.Key, (double)defaultValue);
        textBox.Text = currentValue.ToString();

        // 增减按钮
        var btnDecrease = CreateSpinButton("-");
        var btnIncrease = CreateSpinButton("+");
        Grid.SetColumn(btnDecrease, 1);
        Grid.SetColumn(btnIncrease, 2);

        var step = (int)(item.Step ?? 1);
        var min = (int)(item.Min ?? int.MinValue);
        var max = (int)(item.Max ?? int.MaxValue);

        // 值变更处理（强制取整）
        Action updateValue = () =>
        {
            if (double.TryParse(textBox.Text, out var rawValue))
            {
                var value = (int)Math.Max(min, Math.Min(max, rawValue));
                textBox.Text = value.ToString();
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, value);
                }
            }
        };

        textBox.LostFocus += (s, e) => updateValue();
        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                updateValue();
        };

        btnDecrease.Click += (s, e) =>
        {
            if (int.TryParse(textBox.Text, out var value))
            {
                value = Math.Max(min, value - step);
                textBox.Text = value.ToString();
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, value);
                }
            }
        };

        btnIncrease.Click += (s, e) =>
        {
            if (int.TryParse(textBox.Text, out var value))
            {
                value = Math.Min(max, value + step);
                textBox.Text = value.ToString();
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, value);
                }
            }
        };

        if (!string.IsNullOrEmpty(item.Key))
        {
            _controlMap[item.Key] = textBox;
            _itemMap[item.Key] = item;
        }

        grid.Children.Add(textBox);
        grid.Children.Add(btnDecrease);
        grid.Children.Add(btnIncrease);
        container.Children.Add(grid);
        return container;
    }

    private Button CreateSpinButton(string content)
    {
        var button = new Button { Content = content,
                                  Width = 32,
                                  Height = 32,
                                  Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                                  Foreground = Brushes.White,
                                  BorderThickness = new Thickness(0),
                                  Margin = new Thickness(4, 0, 0, 0),
                                  FontSize = 14,
                                  FontWeight = FontWeights.Bold };

        // 应用圆角样式
        var template = new ControlTemplate(typeof(Button));

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;

        // 触发器
        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), "border"));
        mouseOverTrigger.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
        template.Triggers.Add(mouseOverTrigger);

        var pressedTrigger =
            new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), "border"));
        template.Triggers.Add(pressedTrigger);

        button.Template = template;
        return button;
    }

#endregion

#region CheckBox Rendering

    private FrameworkElement RenderCheckBox(SettingsItem item)
    {
        var container = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

        // 使用 CheckBox 并应用 ToggleSwitchStyle
        var checkBox = new CheckBox { Margin = new Thickness(0, 0, 8, 0) };

        // 尝试应用共享样式，如果不存在则使用回退样式（ToggleButton）
        if (!ApplyStyleIfExists(checkBox, "ToggleSwitchStyle"))
        {
            // 回退到使用 ToggleButton 样式
            var toggle = new ToggleButton { Width = 44, Height = 22, Margin = new Thickness(0, 0, 8, 0) };
            ApplyToggleSwitchStyle(toggle);

            // 加载当前值或默认值
            var defaultValueFallback = item.GetDefaultValue<bool?>() ?? false;
            var currentValueFallback = GetConfigValue(item.Key, defaultValueFallback);
            toggle.IsChecked = currentValueFallback;

            // 值变更事件
            toggle.Checked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, true);
                }
            };
            toggle.Unchecked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    OnValueChanged(item.Key, false);
                }
            };

            if (!string.IsNullOrEmpty(item.Key))
            {
                _controlMap[item.Key] = toggle;
                _itemMap[item.Key] = item;
            }

            container.Children.Add(toggle);

            if (!string.IsNullOrEmpty(item.Label))
            {
                container.Children.Add(new TextBlock { Text = item.Label, Foreground = Brushes.White, FontSize = 12,
                                                       VerticalAlignment = VerticalAlignment.Center });
            }

            return container;
        }

        // 使用共享样式的 CheckBox
        // 加载当前值或默认值
        var defaultValue = item.GetDefaultValue<bool?>() ?? false;
        var currentValue = GetConfigValue(item.Key, defaultValue);
        checkBox.IsChecked = currentValue;

        // 值变更事件
        checkBox.Checked += (s, e) =>
        {
            if (!string.IsNullOrEmpty(item.Key))
            {
                OnValueChanged(item.Key, true);
            }
        };
        checkBox.Unchecked += (s, e) =>
        {
            if (!string.IsNullOrEmpty(item.Key))
            {
                OnValueChanged(item.Key, false);
            }
        };

        if (!string.IsNullOrEmpty(item.Key))
        {
            _controlMap[item.Key] = checkBox;
            _itemMap[item.Key] = item;
        }

        container.Children.Add(checkBox);

        if (!string.IsNullOrEmpty(item.Label))
        {
            container.Children.Add(new TextBlock { Text = item.Label, Foreground = Brushes.White, FontSize = 12,
                                                   VerticalAlignment = VerticalAlignment.Center });
        }

        return container;
    }

    /// <summary>
    /// 回退：应用开关样式到 ToggleButton（用于测试环境）
    /// </summary>
    private void ApplyToggleSwitchStyle(ToggleButton toggle)
    {
        // 简化的开关样式
        toggle.Template = CreateToggleSwitchTemplate();
    }

    private ControlTemplate CreateToggleSwitchTemplate()
    {
        var template = new ControlTemplate(typeof(ToggleButton));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        var trackFactory = new FrameworkElementFactory(typeof(Border));
        trackFactory.Name = "track";
        trackFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
        trackFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));

        var thumbFactory = new FrameworkElementFactory(typeof(Border));
        thumbFactory.Name = "thumb";
        thumbFactory.SetValue(Border.WidthProperty, 18.0);
        thumbFactory.SetValue(Border.HeightProperty, 18.0);
        thumbFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        thumbFactory.SetValue(Border.BackgroundProperty, Brushes.White);
        thumbFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        thumbFactory.SetValue(Border.MarginProperty, new Thickness(2, 0, 0, 0));

        gridFactory.AppendChild(trackFactory);
        gridFactory.AppendChild(thumbFactory);

        template.VisualTree = gridFactory;

        // 触发器
        var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "track"));
        checkedTrigger.Setters.Add(new Setter(Border.MarginProperty, new Thickness(24, 0, 0, 0), "thumb"));
        template.Triggers.Add(checkedTrigger);

        return template;
    }

#endregion

#region ComboBox Rendering

    private FrameworkElement RenderComboBox(SettingsItem item)
    {
        var container = CreateItemContainer(item.Label);

        var comboBox = new ComboBox { MinWidth = 150 };

        // 尝试应用共享样式，如果不存在则使用回退样式
        if (!ApplyStyleIfExists(comboBox, "DarkComboBoxStyle"))
        {
            ApplyDarkComboBoxStyleFallback(comboBox);
        }

        // 添加选项
        if (item.Options != null)
        {
            foreach (var option in item.Options)
            {
                var comboBoxItem = new ComboBoxItem { Content = option.Label, Tag = option.Value };

                // 尝试应用共享样式，如果不存在则使用回退样式
                if (!ApplyStyleIfExists(comboBoxItem, "DarkComboBoxItemStyle"))
                {
                    ApplyDarkComboBoxItemStyleFallback(comboBoxItem);
                }

                comboBox.Items.Add(comboBoxItem);
            }
        }

        // 加载当前值或默认值
        var defaultValue = item.GetDefaultValue<string>() ?? string.Empty;
        var currentValue = GetConfigValue(item.Key, defaultValue);

        // 选中匹配的项
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem cbi && cbi.Tag?.ToString() == currentValue)
            {
                comboBox.SelectedIndex = i;
                break;
            }
        }

        // 值变更事件
        comboBox.SelectionChanged += (s, e) =>
        {
            if (comboBox.SelectedItem is ComboBoxItem selectedItem && !string.IsNullOrEmpty(item.Key))
            {
                OnValueChanged(item.Key, selectedItem.Tag?.ToString() ?? string.Empty);
            }
        };

        if (!string.IsNullOrEmpty(item.Key))
        {
            _controlMap[item.Key] = comboBox;
            _itemMap[item.Key] = item;
        }

        container.Children.Add(comboBox);
        return container;
    }

    /// <summary>
    /// 回退：应用深色主题 ComboBox 样式（用于测试环境）
    /// </summary>
    private void ApplyDarkComboBoxStyleFallback(ComboBox comboBox)
    {
        comboBox.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        comboBox.Foreground = Brushes.White;
        comboBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        comboBox.BorderThickness = new Thickness(1);
        comboBox.Padding = new Thickness(8, 6, 8, 6);
        comboBox.FontSize = 12;
        ApplyDarkComboBoxStyle(comboBox);
    }

    /// <summary>
    /// 回退：应用深色主题 ComboBoxItem 样式（用于测试环境）
    /// </summary>
    private void ApplyDarkComboBoxItemStyleFallback(ComboBoxItem item)
    {
        item.Background = Brushes.Transparent;
        item.Foreground = Brushes.White;
        item.Padding = new Thickness(8, 6, 8, 6);
        ApplyDarkComboBoxItemStyle(item);
    }

    /// <summary>
    /// 应用深色主题 ComboBox 样式
    /// </summary>
    private void ApplyDarkComboBoxStyle(ComboBox comboBox)
    {
        comboBox.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        comboBox.Foreground = Brushes.White;
        comboBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        comboBox.BorderThickness = new Thickness(1);
        comboBox.Padding = new Thickness(8, 6, 8, 6);
        comboBox.FontSize = 12;

        // 创建深色主题模板
        var template = new ControlTemplate(typeof(ComboBox));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // ToggleButton
        var toggleFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton));
        toggleFactory.Name = "ToggleButton";
        toggleFactory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                                 new System.Windows.Data.Binding(
                                     "IsDropDownOpen") { Mode = System.Windows.Data.BindingMode.TwoWay,
                                                         RelativeSource = new System.Windows.Data.RelativeSource(
                                                             System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        toggleFactory.SetValue(System.Windows.Controls.Primitives.ToggleButton.ClickModeProperty, ClickMode.Press);
        toggleFactory.SetValue(System.Windows.Controls.Primitives.ToggleButton.TemplateProperty,
                               CreateToggleButtonTemplate());

        // ContentPresenter
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.Name = "ContentSite";
        contentFactory.SetValue(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("SelectionBoxItem") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        contentFactory.SetValue(
            ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("SelectionBoxItemTemplate") {
                RelativeSource =
                    new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
        contentFactory.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 6, 28, 6));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.IsHitTestVisibleProperty, false);

        // Popup
        var popupFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
        popupFactory.Name = "Popup";
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
                              System.Windows.Controls.Primitives.PlacementMode.Bottom);
        popupFactory.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
                                new System.Windows.Data.Binding(
                                    "IsDropDownOpen") { RelativeSource = new System.Windows.Data.RelativeSource(
                                                            System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.FocusableProperty, false);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty,
                              System.Windows.Controls.Primitives.PopupAnimation.Slide);

        // Popup 内容
        var dropDownGridFactory = new FrameworkElementFactory(typeof(Grid));
        dropDownGridFactory.Name = "DropDown";
        dropDownGridFactory.SetBinding(Grid.MinWidthProperty, new System.Windows.Data.Binding("ActualWidth") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        dropDownGridFactory.SetBinding(Grid.MaxHeightProperty, new System.Windows.Data.Binding("MaxDropDownHeight") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        var dropDownBorderFactory = new FrameworkElementFactory(typeof(Border));
        dropDownBorderFactory.Name = "DropDownBorder";
        dropDownBorderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)));
        dropDownBorderFactory.SetValue(Border.BorderBrushProperty,
                                       new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
        dropDownBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        dropDownBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        dropDownBorderFactory.SetValue(Border.MarginProperty, new Thickness(0, 2, 0, 0));
        dropDownBorderFactory.SetValue(
            Border.EffectProperty,
            new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.3 });

        var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollViewerFactory.SetValue(ScrollViewer.MarginProperty, new Thickness(4));
        scrollViewerFactory.SetValue(ScrollViewer.SnapsToDevicePixelsProperty, true);

        var itemsHostFactory = new FrameworkElementFactory(typeof(StackPanel));
        itemsHostFactory.SetValue(StackPanel.IsItemsHostProperty, true);
        itemsHostFactory.SetValue(System.Windows.Input.KeyboardNavigation.DirectionalNavigationProperty,
                                  System.Windows.Input.KeyboardNavigationMode.Contained);

        scrollViewerFactory.AppendChild(itemsHostFactory);
        dropDownBorderFactory.AppendChild(scrollViewerFactory);
        dropDownGridFactory.AppendChild(dropDownBorderFactory);
        popupFactory.AppendChild(dropDownGridFactory);

        gridFactory.AppendChild(toggleFactory);
        gridFactory.AppendChild(contentFactory);
        gridFactory.AppendChild(popupFactory);

        template.VisualTree = gridFactory;
        comboBox.Template = template;
    }

    /// <summary>
    /// 创建 ComboBox 的 ToggleButton 模板
    /// </summary>
    private ControlTemplate CreateToggleButtonTemplate()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
        borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(20));

        var colDefsFactory = new FrameworkElementFactory(typeof(Grid));
        colDefsFactory.AppendChild(col1);
        colDefsFactory.AppendChild(col2);

        var arrowFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        arrowFactory.Name = "Arrow";
        arrowFactory.SetValue(System.Windows.Shapes.Path.DataProperty,
                              System.Windows.Media.Geometry.Parse("M 0 0 L 4 4 L 8 0 Z"));
        arrowFactory.SetValue(System.Windows.Shapes.Path.FillProperty,
                              new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)));
        arrowFactory.SetValue(System.Windows.Shapes.Path.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        arrowFactory.SetValue(System.Windows.Shapes.Path.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowFactory.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 0, 8, 0));

        gridFactory.AppendChild(arrowFactory);
        borderFactory.AppendChild(gridFactory);

        template.VisualTree = borderFactory;

        // 触发器
        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(
            new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "border"));
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Shapes.Path.FillProperty, Brushes.White, "Arrow"));
        mouseOverTrigger.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
        template.Triggers.Add(mouseOverTrigger);

        var checkedTrigger =
            new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(
            new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "border"));
        checkedTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)), "border"));
        template.Triggers.Add(checkedTrigger);

        return template;
    }

    /// <summary>
    /// 应用深色主题 ComboBoxItem 样式
    /// </summary>
    private void ApplyDarkComboBoxItemStyle(ComboBoxItem item)
    {
        item.Background = Brushes.Transparent;
        item.Foreground = Brushes.White;
        item.Padding = new Thickness(8, 6, 8, 6);

        var template = new ControlTemplate(typeof(ComboBoxItem));

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        borderFactory.AppendChild(contentFactory);

        template.VisualTree = borderFactory;

        // 触发器
        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), "border"));
        mouseOverTrigger.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
        template.Triggers.Add(mouseOverTrigger);

        var selectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "border"));
        template.Triggers.Add(selectedTrigger);

        item.Template = template;
    }

#endregion

#region ProcessList Rendering

    /// <summary>
    /// 渲染进程列表控件
    /// 包含输入框、添加按钮、选取窗口按钮、Popup 进程选择器和已添加进程标签列表
    /// </summary>
    private FrameworkElement RenderProcessList(SettingsItem item)
    {
        var container = CreateItemContainer(item.Label);

        // 输入行容器
        var inputRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 输入框 - 使用共享样式
        var textBox = new TextBox { MinWidth = 150, VerticalContentAlignment = VerticalAlignment.Center };

        // 尝试应用共享样式，如果不存在则使用回退样式
        if (!ApplyStyleIfExists(textBox, "DarkTextBoxStyle"))
        {
            textBox.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            textBox.Foreground = Brushes.White;
            textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            textBox.BorderThickness = new Thickness(1);
            textBox.Padding = new Thickness(8, 6, 8, 6);
            textBox.FontSize = 12;
            ApplyRoundedTextBoxStyle(textBox);
        }

        if (!string.IsNullOrEmpty(item.Placeholder))
        {
            textBox.Tag = item.Placeholder;
        }

        // 添加按钮 - 使用共享样式
        var addButton = CreateProcessListButton("添加");
        addButton.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(addButton, 1);

        // 选取窗口按钮和 Popup - 使用共享样式
        var selectButton = CreateProcessListButton("选取窗口");
        selectButton.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(selectButton, 2);

        // Popup 进程选择器
        var popup = new Popup { PlacementTarget = selectButton, Placement = PlacementMode.Bottom, StaysOpen = false,
                                AllowsTransparency = true };

        var popupBorder =
            new Border { Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                         BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                         BorderThickness = new Thickness(1),
                         CornerRadius = new CornerRadius(4),
                         Margin = new Thickness(0, 4, 0, 0),
                         MaxHeight = 300,
                         MinWidth = 250,
                         Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2,
                                                                                      Opacity = 0.3 } };

        var popupScrollViewer =
            new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(4) };

        var popupList = new StackPanel();
        popupScrollViewer.Content = popupList;
        popupBorder.Child = popupScrollViewer;
        popup.Child = popupBorder;

        // 已添加进程标签列表
        var tagsPanel = new WrapPanel { Orientation = Orientation.Horizontal };

        // 加载当前配置值
        var currentValue = GetConfigValue<string>(item.Key, item.GetDefaultValue<string>() ?? string.Empty);
        var processList = ProcessListHelper.ParseProcessList(currentValue);

        // 辅助方法：更新标签列表
        void UpdateTagsPanel()
        {
            tagsPanel.Children.Clear();
            foreach (var processName in processList)
            {
                var tag = CreateProcessTag(processName, () =>
                                                        {
                                                            processList.Remove(processName);
                                                            UpdateTagsPanel();
                                                            SaveProcessList();
                                                        });
                tagsPanel.Children.Add(tag);
            }
        }

        // 辅助方法：保存进程列表到配置
        void SaveProcessList()
        {
            var serialized = ProcessListHelper.SerializeProcessList(processList);
            if (!string.IsNullOrEmpty(item.Key))
            {
                OnValueChanged(item.Key, serialized);
            }
        }

        // 辅助方法：添加进程
        void AddProcess(string processName)
        {
            var cleaned = processName.Trim();
            if (!string.IsNullOrEmpty(cleaned) && !processList.Contains(cleaned))
            {
                processList.Add(cleaned);
                UpdateTagsPanel();
                SaveProcessList();
            }
        }

        // 初始化标签列表
        UpdateTagsPanel();

        // 添加按钮点击事件
        addButton.Click += (s, e) =>
        {
            AddProcess(textBox.Text);
            textBox.Text = string.Empty;
            textBox.Focus();
        };

        // 输入框回车事件
        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AddProcess(textBox.Text);
                textBox.Text = string.Empty;
            }
        };

        // 选取窗口按钮点击事件
        selectButton.Click += (s, e) =>
        {
            // 刷新进程列表
            popupList.Children.Clear();
            var runningProcesses = ProcessListHelper.GetRunningProcesses();

            if (runningProcesses.Count == 0)
            {
                var emptyLabel = new TextBlock { Text = "没有找到运行中的窗口",
                                                 Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                                                 FontSize = 12, Margin = new Thickness(8, 4, 8, 4) };
                popupList.Children.Add(emptyLabel);
            }
            else
            {
                foreach (var processInfo in runningProcesses)
                {
                    var processItem = CreateProcessListItem(processInfo, () =>
                                                                         {
                                                                             AddProcess(processInfo.ProcessName);
                                                                             popup.IsOpen = false;
                                                                         });
                    popupList.Children.Add(processItem);
                }
            }

            popup.IsOpen = true;
        };

        // 组装控件
        inputRow.Children.Add(textBox);
        inputRow.Children.Add(addButton);
        inputRow.Children.Add(selectButton);

        container.Children.Add(inputRow);
        container.Children.Add(popup);
        container.Children.Add(tagsPanel);

        if (!string.IsNullOrEmpty(item.Key))
        {
            _controlMap[item.Key] = tagsPanel;
            _itemMap[item.Key] = item;
        }

        return container;
    }

    /// <summary>
    /// 创建进程列表按钮 - 使用共享样式
    /// </summary>
    private Button CreateProcessListButton(string content)
    {
        var button =
            new Button { Content = content, Padding = new Thickness(12, 6, 12, 6), FontSize = 12, Height = 32 };

        // 尝试应用共享样式，如果不存在则使用回退样式
        if (!ApplyStyleIfExists(button, "ActionButtonStyle"))
        {
            button.Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            button.Foreground = Brushes.White;
            button.BorderThickness = new Thickness(0);
            ApplyRoundedButtonStyle(button);
        }

        return button;
    }

    /// <summary>
    /// 创建进程标签（带删除按钮）
    /// </summary>
    private Border CreateProcessTag(string processName, Action onRemove)
    {
        var tag = new Border { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                               BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                               BorderThickness = new Thickness(1),
                               CornerRadius = new CornerRadius(4),
                               Margin = new Thickness(0, 0, 8, 8),
                               Padding = new Thickness(8, 4, 4, 4) };

        var content = new StackPanel { Orientation = Orientation.Horizontal };

        var label = new TextBlock { Text = processName, Foreground = Brushes.White, FontSize = 12,
                                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };

        var removeButton = new Button { Content = "×",
                                        Background = Brushes.Transparent,
                                        Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                                        BorderThickness = new Thickness(0),
                                        FontSize = 14,
                                        FontWeight = FontWeights.Bold,
                                        Width = 20,
                                        Height = 20,
                                        Padding = new Thickness(0),
                                        VerticalContentAlignment = VerticalAlignment.Center,
                                        HorizontalContentAlignment = HorizontalAlignment.Center,
                                        Cursor = System.Windows.Input.Cursors.Hand };

        // 应用简单的按钮模板
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;

        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), "border"));
        mouseOverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        template.Triggers.Add(mouseOverTrigger);

        removeButton.Template = template;
        removeButton.Click += (s, e) => onRemove();

        content.Children.Add(label);
        content.Children.Add(removeButton);
        tag.Child = content;

        return tag;
    }

    /// <summary>
    /// 创建 Popup 中的进程列表项
    /// </summary>
    private Border CreateProcessListItem(ProcessInfo processInfo, Action onClick)
    {
        var item = new Border { Background = Brushes.Transparent, Padding = new Thickness(8, 6, 8, 6),
                                CornerRadius = new CornerRadius(3), Cursor = System.Windows.Input.Cursors.Hand };

        var content = new StackPanel();

        var processNameLabel = new TextBlock { Text = processInfo.ProcessName, Foreground = Brushes.White,
                                               FontSize = 12, FontWeight = FontWeights.SemiBold };

        var windowTitleLabel =
            new TextBlock { Text = processInfo.WindowTitle,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), FontSize = 11,
                            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 230 };

        content.Children.Add(processNameLabel);
        if (!string.IsNullOrEmpty(processInfo.WindowTitle))
        {
            content.Children.Add(windowTitleLabel);
        }
        item.Child = content;

        // 鼠标悬停效果
        item.MouseEnter += (s, e) =>
        { item.Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)); };
        item.MouseLeave += (s, e) =>
        { item.Background = Brushes.Transparent; };

        // 点击事件
        item.MouseLeftButtonDown += (s, e) => onClick();

        return item;
    }

#endregion

#region Slider Rendering

    private FrameworkElement RenderSlider(SettingsItem item)
    {
        var container = CreateItemContainer(item.Label);

        var sliderContainer = new Grid();
        sliderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = new Slider { Minimum = item.Min ?? 0,
                                  Maximum = item.Max ?? 100,
                                  TickFrequency = item.Step ?? 1,
                                  IsSnapToTickEnabled = item.Step.HasValue,
                                  MinWidth = 150,
                                  VerticalAlignment = VerticalAlignment.Center };

        // 尝试应用共享样式
        ApplyStyleIfExists(slider, "DarkSliderStyle");

        var valueLabel = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                                         FontSize = 12,
                                         MinWidth = 40,
                                         TextAlignment = TextAlignment.Right,
                                         VerticalAlignment = VerticalAlignment.Center,
                                         Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(valueLabel, 1);

        // 确定显示格式
        var displayFormat = DetermineSliderFormat(item);

        // 加载当前值或默认值
        var defaultValue = item.GetDefaultValue<double?>() ?? item.Min ?? 0;
        var currentValue = GetConfigValue(item.Key, defaultValue);
        slider.Value = currentValue;
        valueLabel.Text = FormatSliderValue(currentValue, displayFormat);

        // 值变更事件
        slider.ValueChanged += (s, e) =>
        {
            valueLabel.Text = FormatSliderValue(slider.Value, displayFormat);
            if (!string.IsNullOrEmpty(item.Key))
            {
                OnValueChanged(item.Key, slider.Value);
            }
        };

        if (!string.IsNullOrEmpty(item.Key))
        {
            _controlMap[item.Key] = slider;
            _itemMap[item.Key] = item;
        }

        sliderContainer.Children.Add(slider);
        sliderContainer.Children.Add(valueLabel);
        container.Children.Add(sliderContainer);
        return container;
    }

    /// <summary>
    /// 确定滑动条的显示格式
    /// </summary>
    /// <param name="item">设置项</param>
    /// <returns>格式类型: "percent", "integer", "decimal"</returns>
    internal static string DetermineSliderFormat(SettingsItem item)
    {
        // 如果明确指定了格式，使用指定的格式
        if (!string.IsNullOrEmpty(item.Format))
        {
            return item.Format.ToLowerInvariant();
        }

        // 自动检测：min=0 且 max<=1 时使用百分比格式
        var min = item.Min ?? 0;
        var max = item.Max ?? 100;
        if (min >= 0 && max <= 1)
        {
            return "percent";
        }

        // 默认使用整数格式
        return "integer";
    }

    /// <summary>
    /// 格式化滑动条值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="format">格式类型</param>
    /// <returns>格式化后的字符串</returns>
    internal static string FormatSliderValue(double value, string format)
    {
        return format switch {
            "percent" => $"{(value * 100):F0}%", "decimal" => value.ToString("F1"),
            _ => value.ToString("F0") // "integer" 或其他
        };
    }

#endregion

#region Button Rendering

    private FrameworkElement RenderButton(SettingsItem item)
    {
        var button = new Button { Content = item.Label ?? "按钮", Padding = new Thickness(16, 8, 16, 8), FontSize = 12,
                                  Margin = new Thickness(0, 8, 0, 0), MinWidth = 80 };

        // 尝试应用共享样式，如果不存在则使用回退样式
        if (!ApplyStyleIfExists(button, "ActionButtonStyle"))
        {
            button.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            button.Foreground = Brushes.White;
            button.BorderThickness = new Thickness(0);
            ApplyRoundedButtonStyle(button);
        }

        button.Click += (s, e) =>
        { OnButtonAction(item.Action ?? string.Empty); };

        return button;
    }

    /// <summary>
    /// 回退：应用圆角按钮样式（用于测试环境）
    /// </summary>
    private void ApplyRoundedButtonStyle(Button button)
    {
        var template = new ControlTemplate(typeof(Button));

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") {
            RelativeSource =
                new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        borderFactory.AppendChild(contentFactory);
        template.VisualTree = borderFactory;

        // 触发器
        var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), "border"));
        mouseOverTrigger.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
        template.Triggers.Add(mouseOverTrigger);

        var pressedTrigger =
            new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(
            new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), "border"));
        template.Triggers.Add(pressedTrigger);

        button.Template = template;
    }

#endregion

#region GroupBox Rendering

    private FrameworkElement RenderGroupBox(SettingsItem item)
    {
        var groupBox = new GroupBox { Header = item.Label,
                                      Foreground = Brushes.White,
                                      BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                                      BorderThickness = new Thickness(1),
                                      Margin = new Thickness(0, 8, 0, 0),
                                      Padding = new Thickness(8) };

        var content = new StackPanel();

        if (item.Items != null)
        {
            foreach (var subItem in item.Items)
            {
                var control = RenderItem(subItem);
                if (control != null)
                {
                    content.Children.Add(control);
                }
            }
        }

        groupBox.Content = content;
        return groupBox;
    }

#endregion

#region Style Helper Methods

    /// <summary>
    /// 从 Application.Current.Resources 获取共享样式
    /// </summary>
    /// <param name="key">样式键名</param>
    /// <returns>找到的样式，如果不存在则返回 null</returns>
    private static Style? GetStyle(string key)
    {
        try
        {
            return Application.Current?.TryFindResource(key) as Style;
        }
        catch
        {
            // 在测试环境中 Application.Current 可能为 null
            return null;
        }
    }

    /// <summary>
    /// 尝试应用共享样式到控件
    /// </summary>
    /// <param name="element">目标控件</param>
    /// <param name="styleKey">样式键名</param>
    /// <returns>是否成功应用样式</returns>
    private static bool ApplyStyleIfExists(FrameworkElement element, string styleKey)
    {
        var style = GetStyle(styleKey);
        if (style != null)
        {
            element.Style = style;
            return true;
        }
        return false;
    }

#endregion

#region Helper Methods

    private StackPanel CreateItemContainer(string? label)
    {
        var container = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        if (!string.IsNullOrEmpty(label))
        {
            var labelBlock = new TextBlock { Text = label };

            // 尝试应用共享样式，如果不存在则使用回退样式
            if (!ApplyStyleIfExists(labelBlock, "SettingsLabelStyle"))
            {
                labelBlock.Foreground = Brushes.White;
                labelBlock.FontSize = 12;
            }
            labelBlock.Margin = new Thickness(0, 0, 0, 4);

            container.Children.Add(labelBlock);
        }

        return container;
    }

    private T GetConfigValue<T>(string? key, T defaultValue)
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;

        return _config.Get(key, defaultValue);
    }

    private void RefreshControlValue(string key, FrameworkElement control, SettingsItem? item = null)
    {
        switch (control)
        {
        case TextBox textBox:
            // 尝试作为数字读取，如果失败则作为字符串读取
            var numValue = _config.Get<double?>(key, null);
            if (numValue.HasValue)
            {
                // 强制取整显示
                textBox.Text = ((int)numValue.Value).ToString();
            }
            else
            {
                // 配置中没有值时，使用 settings_ui.json 中的默认值
                var strValue = _config.Get<string?>(key, null);
                if (strValue != null)
                {
                    textBox.Text = strValue;
                }
                else
                {
                    // 使用 SettingsItem 中的默认值
                    var defaultNum = item?.GetDefaultValue<double?>();
                    if (defaultNum.HasValue)
                    {
                        textBox.Text = ((int)defaultNum.Value).ToString();
                    }
                    else
                    {
                        textBox.Text = item?.GetDefaultValue<string>() ?? string.Empty;
                    }
                }
            }
            break;
        case ToggleButton toggle:
            var boolValue = _config.Get<bool?>(key, null);
            if (boolValue.HasValue)
            {
                toggle.IsChecked = boolValue.Value;
            }
            else
            {
                // 使用 SettingsItem 中的默认值
                toggle.IsChecked = item?.GetDefaultValue<bool?>() ?? false;
            }
            break;
        case ComboBox comboBox:
            var selectValue = _config.Get<string?>(key, null);
            if (selectValue == null)
            {
                // 使用 SettingsItem 中的默认值
                selectValue = item?.GetDefaultValue<string>() ?? string.Empty;
            }
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem cbi && cbi.Tag?.ToString() == selectValue)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
            break;
        case Slider slider:
            var sliderValue = _config.Get<double?>(key, null);
            if (sliderValue.HasValue)
            {
                slider.Value = sliderValue.Value;
            }
            else
            {
                // 使用 SettingsItem 中的默认值
                slider.Value = item?.GetDefaultValue<double?>() ?? item?.Min ?? 0;
            }
            break;
        case WrapPanel tagsPanel:
            // ProcessList 控件：刷新标签列表
            // 注意：由于 ProcessList 使用闭包来管理状态，这里只能重新加载配置值
            // 实际的刷新需要重新渲染整个控件，这里暂时不支持动态刷新
            break;
        }
    }

    private void OnValueChanged(string key, object value)
    {
        _config.Set(key, value);
        ValueChanged?.Invoke(this, new SettingsValueChangedEventArgs(key, value));
    }

    private void OnButtonAction(string action)
    {
        ButtonAction?.Invoke(this, new SettingsButtonActionEventArgs(action));
    }

#endregion
}

#region Event Args

/// <summary>
/// 设置值变更事件参数
/// </summary>
public class SettingsValueChangedEventArgs : EventArgs
{
    public string Key { get; }
    public object Value { get; }

    public SettingsValueChangedEventArgs(string key, object value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// 按钮动作事件参数
/// </summary>
public class SettingsButtonActionEventArgs : EventArgs
{
    public string Action { get; }

    public SettingsButtonActionEventArgs(string action)
    {
        Action = action;
    }
}

#endregion
}
