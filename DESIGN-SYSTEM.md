# TalkKeys Design System

This document describes the TalkKeys design system, which provides a consistent, elegant, and maintainable visual language across all application windows.

## Overview

The design system is centralized in `DesignResources.xaml` and provides:
- **Design Tokens**: Colors, typography, spacing, shadows
- **Component Styles**: Reusable UI component definitions
- **Consistency**: Single source of truth for all visual elements

## Usage

All design tokens are available application-wide through `App.xaml`:

```xaml
<!-- Using a color -->
<Border Background="{StaticResource BackgroundPrimary}"/>

<!-- Using spacing -->
<StackPanel Margin="{StaticResource SpacingLG}"/>

<!-- Using typography -->
<TextBlock FontSize="{StaticResource FontSizeTitle}"/>
```

---

## Color Palette

### Background Colors

| Token | Color | Usage |
|-------|-------|-------|
| `BackgroundPrimary` | `#1A1A1A` | Main window backgrounds |
| `BackgroundSecondary` | `#242424` | Cards, elevated surfaces |
| `BackgroundTertiary` | `#2A2A2A` | Hover states, subtle elevation |
| `BackgroundElevated` | `#1A2332` | Special elevated components |

### Border Colors

| Token | Color | Usage |
|-------|-------|-------|
| `BorderDefault` | `#333333` | Standard borders |
| `BorderInput` | `#444444` | Input field borders |
| `BorderSubtle` | `#2D3748` | Subtle separators |

### Text Colors

| Token | Color | Usage |
|-------|-------|-------|
| `TextPrimary` | `#E5E7EB` | Main content, headings |
| `TextSecondary` | `#D1D5DB` | Labels, secondary content |
| `TextTertiary` | `#9CA3AF` | Descriptions, hints |
| `TextMuted` | `#6B7280` | Placeholder text |
| `TextDisabled` | `#4B5563` | Disabled elements |

### Accent Colors

#### Primary (Indigo)
| Token | Color | Usage |
|-------|-------|-------|
| `AccentPrimary` | `#4F46E5` | Primary actions |
| `AccentPrimaryHover` | `#6366F1` | Hover state |
| `AccentPrimaryPressed` | `#4338CA` | Active/pressed state |
| `AccentPrimaryLight` | `#818CF8` | Light variant |

#### Success (Green)
| Token | Color | Usage |
|-------|-------|-------|
| `AccentSuccess` | `#10B981` | Success states, positive actions |
| `AccentSuccessHover` | `#34D399` | Hover state |
| `AccentSuccessPressed` | `#059669` | Active state |

#### Warning (Amber)
| Token | Color | Usage |
|-------|-------|-------|
| `AccentWarning` | `#F59E0B` | Warning states |
| `AccentWarningHover` | `#FBBF24` | Hover state |
| `AccentWarningPressed` | `#D97706` | Active state |

#### Error (Red)
| Token | Color | Usage |
|-------|-------|-------|
| `AccentError` | `#EF4444` | Error states, destructive actions |
| `AccentErrorHover` | `#F87171` | Hover state |
| `AccentErrorPressed` | `#DC2626` | Active state |

#### Info (Blue)
| Token | Color | Usage |
|-------|-------|-------|
| `AccentInfo` | `#60A5FA` | Informational elements |
| `AccentInfoHover` | `#93C5FD` | Hover state |
| `AccentInfoPressed` | `#3B82F6` | Active state |

### Gradient Brushes

| Token | Description |
|-------|-------------|
| `GradientAccent` | Primary gradient (Indigo → Purple → Pink) |
| `GradientSuccess` | Success gradient (Green shades) |

---

## Typography

### Font Family
- **Base**: `Segoe UI` (`FontFamilyBase`)

### Font Sizes

| Token | Size | Usage |
|-------|------|-------|
| `FontSizeDisplay` | 28px | Page headers, main titles |
| `FontSizeTitle` | 18px | Section titles |
| `FontSizeHeading` | 16px | Subsection headings |
| `FontSizeBodyLarge` | 14px | Important body text, labels |
| `FontSizeBody` | 13px | Default body text, form inputs |
| `FontSizeCaption` | 12px | Captions, descriptions |
| `FontSizeSmall` | 11px | Small text, metadata |

### Example
```xaml
<TextBlock Text="Settings"
           FontSize="{StaticResource FontSizeDisplay}"
           FontWeight="Bold"
           Foreground="{StaticResource TextPrimary}"/>
```

---

## Spacing

### Spacing Scale

| Token | Value | Usage |
|-------|-------|-------|
| `SpacingXS` | 4px | Minimal spacing, tight layouts |
| `SpacingSM` | 8px | Small gaps, compact elements |
| `SpacingMD` | 12px | Default spacing |
| `SpacingLG` | 16px | Comfortable spacing |
| `SpacingXL` | 20px | Large spacing, section padding |
| `Spacing2XL` | 24px | Extra large spacing |
| `Spacing3XL` | 32px | Maximum spacing, page margins |

### Usage Patterns

```xaml
<!-- Card with standard padding -->
<Border Padding="{StaticResource SpacingXL}"/>

<!-- Stack with medium gaps -->
<StackPanel>
    <TextBlock Margin="0,0,0,{StaticResource SpacingMDValue}"/>
    <TextBlock Margin="0,0,0,{StaticResource SpacingMDValue}"/>
</StackPanel>
```

---

## Corner Radius

| Token | Value | Usage |
|-------|-------|-------|
| `CornerRadiusSmall` | 6px | Buttons, inputs, small elements |
| `CornerRadiusMedium` | 8px | Cards, panels |
| `CornerRadiusLarge` | 12px | Windows, large containers |

```xaml
<Border CornerRadius="{StaticResource CornerRadiusMedium}"/>
```

---

## Shadows & Effects

### Elevation Shadows

| Token | Blur | Depth | Opacity | Usage |
|-------|------|-------|---------|-------|
| `ShadowElevation1` | 12px | 2px | 0.1 | Subtle elevation (cards) |
| `ShadowElevation2` | 24px | 4px | 0.2 | Medium elevation (dialogs) |
| `ShadowElevation3` | 32px | 8px | 0.25 | High elevation (modals) |

### Glow Effects

| Token | Color | Usage |
|-------|-------|-------|
| `GlowAccent` | Indigo | Primary action highlights |
| `GlowSuccess` | Green | Success states |
| `GlowError` | Red | Error states |

```xaml
<Border Effect="{StaticResource ShadowElevation2}"/>
```

---

## Animation

### Durations

| Token | Value | Usage |
|-------|-------|-------|
| `AnimationFast` | 150ms | Hover effects, quick transitions |
| `AnimationNormal` | 250ms | Default transitions |
| `AnimationSlow` | 350ms | Complex state changes |

### Recommended Easing
- **Hover**: Ease-out
- **Press**: Ease-in
- **State changes**: Ease-in-out

---

## Component Dimensions

### Buttons

| Token | Value | Usage |
|-------|-------|-------|
| `ButtonHeightSmall` | 32px | Compact buttons |
| `ButtonHeightMedium` | 36px | Standard buttons |
| `ButtonHeightLarge` | 44px | Prominent CTAs |

### Inputs

| Token | Value | Usage |
|-------|-------|-------|
| `InputHeight` | 36px | All form inputs |
| `InputPadding` | 10px 8px | Internal padding |

### Icons

| Token | Value | Usage |
|-------|-------|-------|
| `IconSizeSmall` | 16px | Small icons, indicators |
| `IconSizeMedium` | 20px | Standard icons |
| `IconSizeLarge` | 24px | Large icons, headers |

---

## Best Practices

### 1. Always Use Design Tokens
❌ **Don't**: `<Border Background="#1A1A1A"/>`
✅ **Do**: `<Border Background="{StaticResource BackgroundPrimary}"/>`

### 2. Maintain Consistency
- Use the spacing scale consistently
- Don't create one-off spacing values
- Stick to the defined color palette

### 3. Semantic Colors
- Use `AccentPrimary` for primary actions
- Use `AccentSuccess` for confirmations
- Use `AccentError` for destructive actions

### 4. Accessibility
- Ensure sufficient contrast between text and background
- Provide focus indicators for all interactive elements
- Use proper font sizes (minimum 12px for body text)

### 5. Responsive Spacing
- Use larger spacing on larger screens when appropriate
- Maintain consistent spacing ratios

---

## Component Style Guidelines

(To be expanded in Phase 2)

- Button variants (Primary, Secondary, Accent, Danger, Ghost)
- Input styles (TextBox, ComboBox, CheckBox, RadioButton)
- Container styles (Card, Panel, Dialog)
- Feedback components (ProgressBar, Badge, Toast)

---

## Future Enhancements

- Dark/Light theme switching
- Custom theme support
- Accessibility mode (high contrast)
- Animation preferences
- Density options (compact, comfortable, spacious)

---

**Version**: 1.0
**Last Updated**: Phase 1 Implementation
**Status**: Foundation Complete ✅
