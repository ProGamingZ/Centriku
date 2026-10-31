# Centriku Design System & UI/UX Guidelines

## 1. Design Tokens (CSS & Avalonia XAML Reference)

### 1.1 Color Palette Tokens
| Token Name (Avalonia Key) | CSS Variable Equivalent | Dark Theme (Obsidian Slate) | Light Theme (Soft Alabaster) | Usage |
| :--- | :--- | :--- | :--- | :--- |
| `AppBackground` | `--bg-canvas` | `#0F172A` | `#EDF2F7` | Root window background (eye-strain safe) |
| `SurfaceBrush` | `--bg-surface` | `#1E293B` | `#F8FAFC` | Cards, panels, modals, table base |
| `SurfaceVariantBrush` | `--bg-surface-alt` | `#26334D` | `#E2E8F0` | Table headers, secondary buttons, pill bars |
| `InputBgBrush` | `--bg-input` | `#131C2E` | `#FFFFFF` | TextBox, NumericUpDown, ComboBox rest background |
| `InputFocusBgBrush` | `--bg-input-focus` | `#1A2540` | `#F5F3FF` | Active/focused input field and table cell tint |
| `TextMainBrush` | `--text-primary` | `#F8FAFC` | `#0F172A` | Headings, table body text, primary labels |
| `TextMutedBrush` | `--text-secondary` | `#94A3B8` | `#475569` | Subtitles, field labels, secondary metadata |
| `BorderBrush` | `--border-subtle` | `#334155` | `#CBD5E1` | Internal table row dividers, section rules |
| `BorderStrongBrush` | `--border-strong` | `#475569` | `#94A3B8` | Outer card borders, input borders, header dividers |
| `PrimaryBrush` | `--accent-primary` | `#7C3AED` | `#6D28D9` | Primary CTAs, active tabs, focus rings, checked states |
| `PrimaryHoverBrush` | `--accent-primary-hover` | `#8B5CF6` | `#7C3AED` | Pointer-over state for interactive controls |
| `DataGridAltRowBrush` | `--table-row-alt` | `#172033` | `#F1F5F9` | Odd-numbered zebra striping in DataGrids |
| `DataGridHoverBrush` | `--table-row-hover` | `#28354E` | `#E2E8F0` | Row and dropdown item hover highlight |
| `DataGridSelectedBrush` | `--table-row-selected` | `#3B276B` | `#DDD6FE` | Selected table row and active dropdown item |
| `SuccessBrush` / `SuccessBgBrush` | `--status-success` | `#10B981` / `#2210B981` | `#059669` / `#1F059669` | Passing grades, Restore actions, Healthy status |
| `WarningBrush` / `WarningBgBrush` | `--status-warning` | `#F59E0B` / `#22F59E0B` | `#D97706` / `#1FD97706` | At-Risk alerts, Archive actions, Duplicate warnings |
| `DangerBrush` / `DangerBgBrush` | `--status-danger` | `#F43F5E` / `#22F43F5E` | `#E11D48` / `#1FE11D48` | Critical/FDA failures, Delete/Unenroll actions |

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

### 3.4 Status Badges & Toast Notifications
* **Badges:** Use translucent background tokens (`SuccessBgBrush`, `WarningBgBrush`, `DangerBgBrush`) with a `1px` matching border and bold semantic text (`FontSize="11"`, `CornerRadius="6"`, `Padding="10,4"`).
* **Toasts:** Positioned at `HorizontalAlignment="Right"`, `VerticalAlignment="Bottom"`, `Margin="0,0,30,30"`, featuring a `4px` left accent border (`BorderThickness="4,1,1,1"`) and status icon.

### 3.5 Forms & Inputs
* Always pair inputs with a top-aligned muted label (`FontSize="11"`, `FontWeight="SemiBold"`, `Foreground="{DynamicResource TextMutedBrush}"`, `Margin="0,0,0,4"`).
* Bind all `NumericUpDown.Value` properties through `{StaticResource SafeDoubleConverter}` with `ShowButtonSpinner="False"` to prevent `null` cast crashes on backspace.

---

## 4. Accessibility & Interaction Checklists

- [ ] **Zero Hardcoded Hex Colors in Views:** All `.axaml` views reference `{DynamicResource ...}` tokens from `App.axaml` so switching between Dark and Light mode updates 100% of the UI.
- [ ] **Non-Overlapping Scrollbars:** `ScrollViewer` and `ScrollBar` set `AllowAutoHide="False"` so scrollbars never cover rightmost action buttons or table text.
- [ ] **Destructive Action Confirmation:** Deleting classes, assessments, groups, or archived students always routes through `ConfirmDeleteModalViewModel`.
- [ ] **Empty-Selection Guard:** Bulk toolbar actions (`Archive`, `Restore`, `Delete`, `Transfer`, `Unenroll`) trigger a bottom-right toast alert if executed with `0` rows checked.
- [ ] **Focus & Keyboard Visibility:** Focused `TextBox`, `ComboBox`, and `DataGridCell` elements display a `1.5px–2px` `PrimaryBrush` border and `InputFocusBgBrush` tint.