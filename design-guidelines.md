# Centriku Design System & UI/UX Guidelines

## 1. Design Tokens (CSS & Avalonia XAML Reference)

### 1.1 Color Palette Tokens (NwSSU Institutional Green, Orange & White)
| Token Name (Avalonia Key) | CSS Variable Equivalent | Dark Theme (Forest Obsidian) | Light Theme (NwSSU Alabaster & Green) | Usage |
| :--- | :--- | :--- | :--- | :--- |
| `AppBackground` | `--bg-canvas` | `#09140E` | `#EFF5F1` | Root window background (eye-strain safe sage-alabaster / deep forest) |
| `SurfaceBrush` | `--bg-surface` | `#112218` | `#FFFFFF` | Cards, panels, modals, table base |
| `SurfaceVariantBrush` | `--bg-surface-alt` | `#193124` | `#E3EFE7` | Table headers, secondary buttons, pill bars |
| `InputBgBrush` | `--bg-input` | `#0C1A12` | `#FFFFFF` | TextBox, NumericUpDown, ComboBox rest background |
| `InputFocusBgBrush` | `--bg-input-focus` | `#142E20` | `#F0FDF4` | Active/focused input field and table cell tint |
| `TextMainBrush` | `--text-primary` | `#F2F9F5` | `#0D1F14` | Headings, table body text, primary labels |
| `TextMutedBrush` | `--text-secondary` | `#86A895` | `#3F5A49` | Subtitles, field labels, secondary metadata |
| `BorderBrush` | `--border-subtle` | `#234231` | `#C5D8CC` | Internal table row dividers, section rules |
| `BorderStrongBrush` | `--border-strong` | `#346148` | `#86A893` | Outer card borders, input borders, header dividers |
| `PrimaryBrush` | `--accent-primary` | `#16A34A` | `#166534` | NwSSU Forest Green: Primary CTAs, active tabs, focus rings, checked states |
| `PrimaryHoverBrush` | `--accent-primary-hover` | `#22C55E` | `#15803D` | Pointer-over state for interactive controls |
| `PrimaryBgBrush` | `--accent-primary-bg` | `#2616A34A` | `#1F166534` | Tinted primary badges (e.g., Student Count pill on Class Cards) |
| `AccentBrush` / `AccentBgBrush` | `--accent-secondary` | `#FB923C` / `#24FB923C` | `#EA580C` / `#1FEA580C` | 10% Secondary Brand Callout: Brand badge, Student Count pills, Step numbers, Question counter |
| `DataGridAltRowBrush` | `--table-row-alt` | `#0D1B13` | `#F4F9F5` | Odd-numbered zebra striping in DataGrids |
| `DataGridHoverBrush` | `--table-row-hover` | `#1C3829` | `#E3EFE7` | Row and dropdown item hover highlight |
| `DataGridSelectedBrush` | `--table-row-selected` | `#1E4631` | `#DCFCE7` | Selected table row and active dropdown item |
| `SuccessBrush` / `SuccessBgBrush` | `--status-success` | `#22C55E` / `#2222C55E` | `#15803D` / `#1F15803D` | Passing grades, Restore actions, Healthy status |
| `WarningBrush` / `WarningBgBrush` | `--status-warning` | `#F97316` / `#24F97316` | `#EA580C` / `#1FEA580C` | NwSSU Vibrant Orange: At-Risk alerts, Archive actions, Leader badges |
| `DangerBrush` / `DangerBgBrush` | `--status-danger` | `#F43F5E` / `#22F43F5E` | `#DC2626` / `#1FDC2626` | Critical/FDA failures, Delete/Unenroll actions |
| `InfoBrush` / `InfoBgBrush` | `--status-info` | `#38BDF8` / `#2238BDF8` | `#0284C7` / `#1F0284C7` | Transfer actions and informational category badges |
| `TsColumnBrush` | `--table-col-ts` | `#2DD4BF` | `#0F766E` | Transmuted Score (`TS`) calculated column header and cells |
| `WsColumnBrush` | `--table-col-ws` | `#FB923C` | `#EA580C` | NwSSU Orange Weighted Score (`WS`) calculated column header and cells |


### 1.2 Typography Scale
* **Font Family:** `Inter` (UI Body/Headings), `FluentSystemIcons-Regular` (Icons), `Consolas, Courier New, monospace` (Developer Diagnostics).
* **Page Title (`H1`):** `32px`, `Bold` (`700`), `TextMainBrush`.
* **Section / Modal Header (`H2`):** `20px–22px`, `Bold` (`700`), `TextMainBrush`.
* **Card Title (`H3`):** `16px`, `Bold` (`700`), `TextMainBrush`.
* **Body / Table Cell:** `13px–14px`, `Regular` (`400`) or `SemiBold` (`600`), `TextMainBrush`.
* **Field Label / Table Header:** `11px–12px`, `SemiBold` (`600`) or `Bold` (`700`), `TextMutedBrush`.

### 1.3 Spacing, Border Radius & Shadows
* **Page Outer Margin:** `24px` horizontal, `20px–24px` vertical (`Margin="24,20,24,24"`).
* **Card Internal Padding:** `20px–25px` (`Padding="20"`).
* **Grid / Stack Gap:** `12px–18px` between cards (`Spacing="16"` or `Margin="0,0,16,0"`).
* **Border Radius Scale:**
  * Small Controls (CheckBoxes, Status Badges): `4px–6px`
  * Inputs, Dropdowns, Buttons: `8px`
  * DataGrids & Containers: `10px`
  * Elevated Cards & Modals: `12px`
* **Elevation Shadows:**
  * Floating Modals: `BoxShadow="0 10 30 0 #40000000"`
  * Bottom-Right Toast Notifications: `BoxShadow="0 10 25 0 #66000000"`

### 1.4 Multi-Theme Architecture & Eye-Comfort Dimmer (`ThemeService`)
* **60–30–10 Color Rule:** Every theme preset balances **60% Neutral Surfaces** (`AppBackground`, `SurfaceBrush`), **30% Primary Brand Identity** (`PrimaryBrush`), and **10% Secondary Callout Accents** (`AccentBrush`, `AccentBgBrush`).
* **Built-In Theme Presets (`ThemeService.cs`):**
  1. `NwSSU Forest` (Default Institutional Green `#166534` & Vibrant Orange `#EA580C`)
  2. `Royal Purple` (Classic Centriku Violet `#6D28D9` & Amber Gold `#D97706`)
  3. `Ocean Blue` (Corporate Blue `#2563EB` & Teal `#0D9488`)
  4. `Teal & Slate` (Deep Teal `#0F766E` & Coral Orange `#EA580C`)
  5. `Crimson Rose` (Crimson `#BE123C` & Warm Gold `#D97706`)
  6. `Sakura Blossom` (Cherry Blossom Pink `#DB2777` & Warm Gold `#D97706`)
  7. `Imperial Gold` (Burnished Luxury Gold `#B45309` / `#D97706` & Royal Amethyst / Champagne Accents)
  8. `Sunset Terracotta` (Warm Espresso-Terracotta `#C2410C` & Teal `#0F766E`)
  9. `Nordic Slate` (Minimalist Graphite `#334155` / `#38BDF8` & Sky Blue / Amber Accents)
* **Adding/Removing Themes:** Edit the `ThemeService.Palettes` collection in `Centriku/Services/ThemeService.cs`; the sidebar Appearance Flyout populates options dynamically.
* **Screen Brightness Dimmer:** `MainWindow.axaml` hosts a top-level non-interactive overlay (`IsHitTestVisible="False"`, `ZIndex="9999"`) controlled by `ScreenBrightness` (`50%–100%`) to reduce glare without lowering text-to-background contrast ratios.
---

## 2. Layout Architecture

* **Window Constraints:** Minimum viewport `1024x700` (`MainWindowViewModel`), default `Maximized`.
* **Sidebar Navigation:** Collapsible left navigation bar (`IsSidebarOpen`), active view highlighted via semantic state trackers (`IsDashboardActive`, `IsMyClassesActive`, `IsDirectoryActive`, etc.).
* **Pinned Viewport Pattern (No Outer ScrollViewer on Table Views):**
  * Views hosting a primary `DataGrid` (`MasterRosterTab`, `ArchivesTab`, `GradesTabView`, `AttendanceTabView`) MUST use `<Grid RowDefinitions="Auto, *">`.
  * `Grid.Row="0"` holds the static Header, Search, and Filter toolbar.
  * `Grid.Row="1"` (`*`) holds the `DataGrid` so UI row virtualization stays active and vertical scrollbars remain inside the table.
* **Responsive Breakpoints & Multi-Card Grids:**
  * Settings and configuration pages (`ImportSettingsView`, `ExportSettingsView`) use proportional star columns (`ColumnDefinitions="*, *, *"`) rather than fixed pixel widths so cards scale cleanly from `1024px` laptops to `2560px` ultrawide monitors.
  * **Recitation 3-Column Workspace:** `RecitationTabView` uses a proportional `1* : 1.6* : 1*` grid without fixed `MinWidth` scroll wrappers so the student lists and `Viewbox` wheel scale fluidly down to `1024x700`.
---

## 3. Reusable Component Guidelines

### 3.1 Buttons
* **Primary Button:** `Background="{DynamicResource PrimaryBrush}"`, `Foreground="White"`, `FontWeight="Bold"`, `CornerRadius="8"`, `Padding="20,10"`.
* **Secondary / Utility Button:** `Background="{DynamicResource SurfaceVariantBrush}"`, `Foreground="{DynamicResource TextMainBrush}"`, `BorderBrush="{DynamicResource BorderStrongBrush}"`, `BorderThickness="1"`.
* **Semantic Outline Button (Archive / Restore / Delete / Unenroll):** `Background="Transparent"`, `BorderThickness="1.5"`, `Foreground` and `BorderBrush` bound to `WarningBrush`, `SuccessBrush`, or `DangerBrush`.

### 3.2 Cards & Panels
* Always wrap distinct functional groups in `<Border Background="{DynamicResource SurfaceBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1" CornerRadius="12" Padding="20">`.
* Separate card titles from body controls using `<Border Height="1" Background="{DynamicResource BorderBrush}"/>`.

### 3.3 Data Tables (`DataGrid`)
* **Row Height:** `36px–40px` (`RowHeight="40"`).
* **Column Sizing:** Use fixed widths for compact metadata (`Checkbox = 48px`, `Suffix = 75px`, `Section = 85px`, `Year = 95px`, `Status = 105px`, `Actions = 90px`) and star sizing (`Width="1.1*"` to `1.4*"`) with `MinWidth` guards for names and IDs.
* **Truncation:** Every read-only text cell must include `TextTrimming="CharacterEllipsis"` and `ToolTip.Tip="{Binding Property}"`.
* **Column Locking:** Set `CanUserResizeColumns="False"` and `CanUserReorderColumns="False"` on proportional star tables. Reserve `FrozenColumnCount="4"` strictly for wide horizontal-scrolling sheets (`GradesTabView`, `AttendanceTabView`).
* **Dynamic Table Counter:** Every `DataGrid` view (`MasterRosterTab`, `ArchivesTab`, `BulkImportTab`) must display a right-aligned `SemiBold` counter (`DynamicStudentCounter`, `DynamicArchiveCounter`, `DynamicImportCounter`) using `Foreground="{DynamicResource TextMutedBrush}"` directly above the top-right corner of the table.
* **Inline Cell TextBox Reset:** Single-line `TextBox` controls must globally set `ScrollViewer.VerticalScrollBarVisibility="Disabled"` and `ScrollViewer.HorizontalScrollBarVisibility="Hidden"` in `App.axaml` so inline editing inside `DataGridTextColumn` or `DataGridTemplateColumn` never renders an internal scrollbar thumb inside the cell.
* **Dynamic FrozenColumnCount:** Horizontal-scrolling sheets (`GradesTabView`, `AttendanceTabView`) must dynamically compute `grid.FrozenColumnCount` in code-behind based on the count of currently visible static identity/summary columns so hiding columns in `View Options` never freezes dynamic assessment or date columns.
* **Frameless Inline Cell Editor:** `DataGridCell TextBox` must set `BorderThickness="0"`, `CornerRadius="0"`, and `Background="Transparent"` across normal, `:pointerover`, and `:focus` states so inline editing relies solely on the parent `DataGridCell:focus-within` border without rendering a second rounded box inside the cell. All `TextBox` controls must set `SelectionForegroundBrush="White"`.

### 3.4 Status Badges & Toast Notifications
* **Badges:** Use translucent background tokens (`SuccessBgBrush`, `WarningBgBrush`, `DangerBgBrush`) with a `1px` matching border and bold semantic text (`FontSize="11"`, `CornerRadius="6"`, `Padding="10,4"`).
* **Toasts:** Positioned at `HorizontalAlignment="Right"`, `VerticalAlignment="Bottom"`, `Margin="0,0,30,30"`, featuring a `4px` left accent border (`BorderThickness="4,1,1,1"`) and status icon.

### 3.5 Forms & Inputs
* Always pair inputs with a top-aligned muted label (`FontSize="11"`, `FontWeight="SemiBold"`, `Foreground="{DynamicResource TextMutedBrush}"`, `Margin="0,0,0,4"`).
* Bind all `NumericUpDown.Value` properties through `{StaticResource SafeDoubleConverter}` with `ShowButtonSpinner="False"` to prevent `null` cast crashes on backspace.
* **Popups (`FlyoutPresenter`) & Dropdowns (`ComboBox`):** `App.axaml` and `ThemeService.cs` style `FlyoutPresenter` (`Background="{DynamicResource SurfaceBrush}"`, `BorderBrush="{DynamicResource BorderStrongBrush}"`, `CornerRadius="12"`) and override Avalonia `FluentTheme`'s internal `ComboBox*`, `CheckBox*`, `ToggleSwitch*`, and `Slider*` resource keys so all popups and dropdown menus dynamically adapt to every theme preset.
---

## 4. Accessibility & Interaction Checklists

- [ ] **Zero Hardcoded Hex Colors in Views:** All `.axaml` views reference `{DynamicResource ...}` tokens from `App.axaml` so switching between Dark and Light mode updates 100% of the UI.
- [ ] **Non-Overlapping Scrollbars:** `ScrollViewer` and `ScrollBar` set `AllowAutoHide="False"` so scrollbars never cover rightmost action buttons or table text.
- [ ] **Destructive Action Confirmation:** Deleting classes, assessments, groups, or archived students always routes through `ConfirmDeleteModalViewModel`.
- [ ] **Empty-Selection Guard:** Bulk toolbar actions (`Archive`, `Restore`, `Delete`, `Transfer`, `Unenroll`) trigger a bottom-right toast alert if executed with `0` rows checked.
- [ ] **Focus & Keyboard Visibility:** Focused `TextBox`, `ComboBox`, and `DataGridCell` elements display a `1.5px–2px` `PrimaryBrush` border and `InputFocusBgBrush` tint.