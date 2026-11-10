"""Apply the shared WPF theme while preserving menu order and business bindings.

The snapshot/manifest are also inputs to VerifyDesktopDesign.py. Run once per design.
"""
from pathlib import Path
from lxml import etree as E
import copy, json, re, datetime

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / 'Materijalno.UI'
if 'FinanceTheme.xaml' in (UI/'App.xaml').read_text(encoding='utf-8-sig'):
    raise SystemExit('Theme already applied; use the saved snapshot for verification.')
RUN = ROOT / 'artifacts' / ('DesktopDesign_' + datetime.datetime.now().strftime('%Y%m%d_%H%M%S'))
RUN.mkdir(parents=True)
(ROOT / 'artifacts/current_design_run.txt').write_text(str(RUN), encoding='utf-8')
NS = {'p':'http://schemas.microsoft.com/winfx/2006/xaml/presentation', 'x':'http://schemas.microsoft.com/winfx/2006/xaml'}
P, X = '{'+NS['p']+'}', '{'+NS['x']+'}'
parser = E.XMLParser(remove_blank_text=False)

def snapshot(path):
    dest=RUN/'before'/path.relative_to(ROOT)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(path.read_bytes())

def write(path, tree):
    path.write_bytes(E.tostring(tree, encoding='utf-8', xml_declaration=False, pretty_print=False))

def alias(style, base):
    for child in list(style): style.remove(child)
    style.set('BasedOn', '{StaticResource '+base+'}')

palette={'#F4F7FB':'#F1F5F7', '#2563EB':'#087F82', '#1D4ED8':'#06686C', '#1E40AF':'#07575C',
         '#EEF5FF':'#E7F3F2', '#1F2937':'#203447', '#667085':'#596D7E', '#E4E7EC':'#D7E1E7',
         '#D8E0EA':'#CDD8E0', '#D5DFEC':'#CDD8E0', '#F2F6FC':'#EEF7F6', '#E5EEFB':'#D8EFEB',
         '#17273C':'#203447', '#64748B':'#596D7E', '#DCE9FF':'#D8EFEB', '#E8EEF7':'#DFEEEF'}
aliases={'FormLabelStyle':'FinanceLabel', 'FormTextBoxStyle':'FinanceTextBox',
         'FormDatePickerStyle':'FinanceDatePicker', 'PrimaryButtonStyle':'FinancePrimaryButton',
         'SecondaryButtonStyle':'FinanceButton', 'PrimaryActionButtonStyle':'FinancePrimaryButton',
         'ActionButtonStyle':'FinanceButton', 'DeleteActionButtonStyle':'FinanceDangerButton',
         'TableButtonStyle':'FinanceButton'}
numeric=re.compile(r'\.(?:Kolic\w*|Nc|Vrijed\w*|Fvrijed|Porppp|Trospe|Cartro|Zavtro|Ppp)(?:[,}\s]|$)', re.I)

for path in sorted(UI.glob('*.xaml')):
    snapshot(path)
    tree=E.parse(str(path), parser); root=tree.getroot()
    if path.name=='App.xaml':
        res=root.find(P+'Application.Resources')
        res.append(E.fromstring(('''<ResourceDictionary xmlns="%s"><ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="/Materijalno.UI;component/Themes/FinanceTheme.xaml"/>
        </ResourceDictionary.MergedDictionaries></ResourceDictionary>'''%NS['p']).encode()))
        write(path,tree); continue
    if path.name=='GlavniView.xaml': continue
    if root.tag != P+'UserControl': continue
    resources=root.find(P+'UserControl.Resources')
    if resources is None:
        resources=E.Element(P+'UserControl.Resources'); root.insert(0, resources)
    dictionary=resources.find(P+'ResourceDictionary')
    if dictionary is None:
        dictionary=E.Element(P+'ResourceDictionary')
        for c in list(resources): resources.remove(c); dictionary.append(c)
        resources.append(dictionary)
    merged=E.Element(P+'ResourceDictionary.MergedDictionaries')
    rd=E.SubElement(merged,P+'ResourceDictionary'); rd.set('Source','/Materijalno.UI;component/Themes/FinanceTheme.xaml')
    dictionary.insert(0,merged)
    for el in list(root.iter()):
        if not isinstance(el.tag,str): continue
        for key,value in list(el.attrib.items()):
            if value.upper() in palette: el.set(key,palette[value.upper()])
        if el.tag==P+'Style':
            key=el.get(X+'Key'); target=el.get('TargetType','').replace('{x:Type ','').replace('}','')
            if key in aliases:
                # Keep icon button hit targets and established form/date dimensions.
                dims=[copy.deepcopy(c) for c in el if c.tag==P+'Setter' and c.get('Property') in ('Width','Height','MinWidth','Margin')]
                alias(el,aliases[key])
                for d in dims:
                    if d.get('Property')=='Height' and float(d.get('Value','0'))>48: d.set('Value','40')
                    el.append(d)
            elif target in ('GridViewColumnHeader','ListViewItem'):
                alias(el,'FinanceColumnHeader' if target=='GridViewColumnHeader' else 'FinanceListRow')
            elif target in ('TextBox','DatePicker','Button','ListView') and not el.get('BasedOn'):
                el.set('BasedOn','{StaticResource Finance'+{'TextBox':'TextBox','DatePicker':'DatePicker','Button':'Button','ListView':'ListView'}[target]+'}')
        if el.tag in (P+'TextBox',P+'Label',P+'Button') and el.get('FontSize') in ('16','18'):
            el.set('FontSize','14' if el.tag==P+'TextBox' else '13')
        if el.tag==P+'TextBox' and numeric.search(el.get('Text','')):
            el.set('TextAlignment','Right'); el.set('Typography.NumeralAlignment','Tabular')
        if el.tag==P+'Button' and not el.get('Style') and not el.find(P+'Button.Style') is not None:
            content=el.get('Content','').strip().lower()
            if content in ('snimi','spasi','sačuvaj','formiraj','štampa','štampaj','dodaj','unesi'):
                el.set('Style','{StaticResource FinancePrimaryButton}')
            elif content in ('brisanje','obriši'):
                el.set('Style','{StaticResource FinanceDangerButton}')
        if el.tag==P+'ListView': el.set('AlternationCount','2')
        if el.tag==P+'GridViewColumn' and numeric.search(el.get('DisplayMemberBinding','')):
            binding=el.attrib.pop('DisplayMemberBinding')
            ct=E.SubElement(el,P+'GridViewColumn.CellTemplate'); dt=E.SubElement(ct,P+'DataTemplate')
            tx=E.SubElement(dt,P+'TextBlock',Text=binding,HorizontalAlignment='Stretch',TextAlignment='Right',Margin='10,0',**{'Typography.NumeralAlignment':'Tabular'})
    # Give the compact transaction forms a single calm surface and stronger spacing.
    scroll=root.find(P+'ScrollViewer')
    if scroll is not None:
        scroll.set('Background','#F1F5F7'); panel=scroll.find(P+'StackPanel')
        if panel is not None:
            panel.set('Margin','28,24')
            for wrap in panel.findall(P+'WrapPanel'):
                if wrap.get('Margin')=='0,0,0,6': wrap.set('Margin','0,0,0,12')
    write(path,tree)

path=UI/'GlavniView.xaml'; tree=E.parse(str(path),parser); root=tree.getroot()
resources=root.find(P+'UserControl.Resources')
templates=[copy.deepcopy(c) for c in resources if c.tag in (P+'DataTemplate',P+'BooleanToVisibilityConverter')]
menu=root.xpath('.//p:StackPanel[@x:Name="glavniStackPanel"]',namespaces=NS)[0]
menu=copy.deepcopy(menu)
for el in menu.iter():
    if not isinstance(el.tag,str): continue
    if el.tag==P+'Button' and el.get('Height'): el.attrib.pop('Height')
    if el.tag==P+'TextBlock': el.set('TextWrapping','Wrap')
manifest=[]
for el in menu.iter():
    if el.tag==P+'Button': manifest.append({'name':el.get(X+'Name'), 'content':el.get('Content') or ''.join(el.xpath('.//p:TextBlock/@Text',namespaces=NS)), 'command':el.get('Command')})
    elif el.tag==P+'Expander': manifest.append({'header':el.find(P+'Expander.Header').find(P+'TextBlock').get('Text')})
(RUN/'menu_before.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
for c in list(root): root.remove(c)
res=E.SubElement(root,P+'UserControl.Resources'); rd=E.SubElement(res,P+'ResourceDictionary')
merged=E.SubElement(rd,P+'ResourceDictionary.MergedDictionaries'); E.SubElement(merged,P+'ResourceDictionary',Source='/Materijalno.UI;component/Themes/FinanceTheme.xaml')
for t in templates: rd.append(t)
shellresources=E.fromstring(('''<ResourceDictionary xmlns="%s" xmlns:x="%s">
 <Style x:Key="SidebarButtonStyle" TargetType="Button" BasedOn="{StaticResource FinanceButton}">
  <Setter Property="Foreground" Value="#CDD9E2"/><Setter Property="Background" Value="Transparent"/><Setter Property="BorderBrush" Value="Transparent"/>
  <Setter Property="FontWeight" Value="Normal"/><Setter Property="FontSize" Value="13"/><Setter Property="MinHeight" Value="38"/>
  <Setter Property="Margin" Value="8,2"/><Setter Property="Padding" Value="12,9"/><Setter Property="HorizontalContentAlignment" Value="Left"/>
  <Setter Property="ContentTemplate"><Setter.Value><DataTemplate><TextBlock Text="{Binding}" TextWrapping="Wrap"/></DataTemplate></Setter.Value></Setter>
  <Style.Triggers>
   <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#203E51"/><Setter Property="Foreground" Value="White"/></Trigger>
   <Trigger Property="Tag" Value="Selected"><Setter Property="Background" Value="#186467"/><Setter Property="Foreground" Value="White"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="BorderBrush" Value="#4FA7A2"/></Trigger>
  </Style.Triggers>
 </Style>
 <Style x:Key="SidebarSubButtonStyle" TargetType="Button" BasedOn="{StaticResource SidebarButtonStyle}"><Setter Property="Margin" Value="20,2,8,2"/><Setter Property="FontSize" Value="12.5"/></Style>
 <Style x:Key="SidebarExpanderStyle" TargetType="Expander">
  <Setter Property="Margin" Value="6,3"/><Setter Property="Foreground" Value="#E1E9EE"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Expander">
   <StackPanel><ToggleButton x:Name="HeaderToggle" Foreground="{TemplateBinding Foreground}" Background="Transparent" BorderThickness="0" Padding="12,12" HorizontalContentAlignment="Stretch" Cursor="Hand" IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}">
    <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border x:Name="HeaderBorder" Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="18"/></Grid.ColumnDefinitions><ContentPresenter VerticalAlignment="Center"/><TextBlock x:Name="Arrow" Grid.Column="1" Text="›" FontSize="17" HorizontalAlignment="Right" Foreground="#89A4B5"/></Grid></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="Text" Value="⌄"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="HeaderBorder" Property="Background" Value="#203E51"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="HeaderBorder" Property="Background" Value="#203E51"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
    <ContentPresenter Content="{TemplateBinding Header}"/>
   </ToggleButton><ContentPresenter x:Name="ExpandSite" Visibility="Collapsed"/></StackPanel>
   <ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="ExpandSite" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style x:Key="SidebarSubExpanderStyle" TargetType="Expander" BasedOn="{StaticResource SidebarExpanderStyle}"><Setter Property="Margin" Value="14,1,4,1"/><Setter Property="Foreground" Value="#B6C9D6"/></Style>
</ResourceDictionary>'''%(NS['p'],NS['x'])).encode())
for c in list(shellresources): shellresources.remove(c); rd.append(c)
layout=E.fromstring(('''<Grid xmlns="%s" xmlns:x="%s" Background="#F1F5F7" TextElement.FontFamily="Segoe UI" TextElement.Foreground="#203447">
 <Grid.ColumnDefinitions><ColumnDefinition Width="286"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
 <Border Background="#142D40"><Grid><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
  <Border Padding="24,26,20,23" BorderBrush="#2A4355" BorderThickness="0,0,0,1"><StackPanel><StackPanel Orientation="Horizontal"><Border Background="#087F82" CornerRadius="6" Width="32" Height="32" Margin="0,0,11,0"><TextBlock Text="M" Foreground="White" FontSize="20" FontWeight="SemiBold" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><TextBlock Text="MATERIJALNO" Foreground="White" FontSize="19" FontWeight="SemiBold" VerticalAlignment="Center"/></StackPanel><TextBlock Text="Materijalno knjigovodstvo" Foreground="#9EB5C5" FontSize="12" Margin="43,5,0,0"/></StackPanel></Border>
  <ScrollViewer x:Name="MenuScroll" Grid.Row="1" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Margin="5,12,5,8"/>
  <Border Grid.Row="2" Padding="22,16" BorderBrush="#2A4355" BorderThickness="0,1,0,0"><StackPanel><TextBlock Text="LUTRIJA BIH  /  INTERNO" Foreground="#B7C9D5" FontSize="10" FontWeight="SemiBold"/><TextBlock Text="SVI DOKUMENTI I PODACI MOGU SE KORISTITI SAMO ZA INTERNU UPOTREBU!" TextWrapping="Wrap" Foreground="#91AABB" FontSize="9.5" Margin="0,6,0,0"/></StackPanel></Border>
 </Grid></Border>
 <Grid Grid.Column="1"><Grid.RowDefinitions><RowDefinition Height="76"/><RowDefinition Height="*"/><RowDefinition Height="32"/></Grid.RowDefinitions>
  <Border Background="White" BorderBrush="#D7E1E7" BorderThickness="0,0,0,1" Padding="28,0"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><StackPanel VerticalAlignment="Center"><TextBlock x:Name="SectionText" Text="RADNI PROSTOR" Foreground="#596D7E" FontSize="10" FontWeight="SemiBold"/><TextBlock x:Name="PageText" Text="Pregled rada" FontSize="18" FontWeight="SemiBold" Margin="0,4,16,0" TextTrimming="CharacterEllipsis"/></StackPanel><StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center"><Border Background="#E7F3F2" CornerRadius="5" Padding="10,6" Margin="0,0,18,0"><TextBlock x:Name="EnvironmentText" Text="MATERIJALNO" FontSize="10" FontWeight="SemiBold" Foreground="#06686C"/></Border><TextBlock x:Name="TodayText" FontSize="12" Foreground="#596D7E" VerticalAlignment="Center"/></StackPanel></Grid></Border>
  <Grid Grid.Row="1" Margin="20"><Border x:Name="WelcomePanel" Background="White" BorderBrush="#D7E1E7" BorderThickness="1" CornerRadius="10" Padding="36" VerticalAlignment="Top"><StackPanel><TextBlock Text="Sve za vaš radni dan." FontSize="28" FontWeight="SemiBold" Margin="0,2,0,12"/><TextBlock Text="Odaberite stavku u meniju za unos, pregled ili štampanje podataka." Foreground="#596D7E" FontSize="14" TextWrapping="Wrap"/><Border Height="1" Background="#E4ECEF" Margin="0,28"/><TextBlock Text="PREGLEDAN RAD S PODACIMA" Foreground="#087F82" FontSize="11" FontWeight="SemiBold"/><UniformGrid Columns="3" Margin="0,20,0,10"><StackPanel Margin="0,0,24,0"><TextBlock Text="01  Šifarnici" FontSize="16" FontWeight="SemiBold"/><TextBlock Text="Materijali, skladišta i konta na jednom mjestu." Foreground="#596D7E" TextWrapping="Wrap" FontSize="13" Margin="0,9,0,0"/></StackPanel><StackPanel Margin="0,0,24,0"><TextBlock Text="02  Promet" FontSize="16" FontWeight="SemiBold"/><TextBlock Text="Unos i pregled dokumenata uz jasno izdvojene brojčane podatke." Foreground="#596D7E" TextWrapping="Wrap" FontSize="13" Margin="0,9,0,0"/></StackPanel><StackPanel><TextBlock Text="03  Izvještaji" FontSize="16" FontWeight="SemiBold"/><TextBlock Text="Pregledi, finansijski nalozi i inventura kroz postojeći meni." Foreground="#596D7E" TextWrapping="Wrap" FontSize="13" Margin="0,9,0,0"/></StackPanel></UniformGrid></StackPanel></Border><Border x:Name="WorkspacePanel" Background="White" BorderBrush="#D7E1E7" BorderThickness="1" CornerRadius="10" Visibility="Collapsed" ClipToBounds="True"><ContentControl x:Name="odabraniContent" Content="{Binding OdabraniVM}"/></Border></Grid>
  <Grid Grid.Row="2" Margin="26,0"><TextBlock Text="Materijalno knjigovodstvo" Foreground="#596D7E" FontSize="10.5" VerticalAlignment="Center"/><TextBlock Text="Interna upotreba" Foreground="#596D7E" FontSize="10.5" HorizontalAlignment="Right" VerticalAlignment="Center"/></Grid>
 </Grid>
</Grid>'''%(NS['p'],NS['x'])).encode())
layout.xpath('.//p:ScrollViewer[@x:Name="MenuScroll"]',namespaces=NS)[0].append(menu)
root.append(layout); root.set('UseLayoutRounding','True'); root.set('SnapsToDevicePixels','True')
write(path,tree)
project=UI/'Materijalno.UI.csproj'; snapshot(project)
text=project.read_text(encoding='utf-8-sig')
text=text.replace('    <Page Include="GlavniView.xaml">','    <Page Include="Themes\\FinanceTheme.xaml"><Generator>MSBuild:Compile</Generator><SubType>Designer</SubType></Page>\n    <Page Include="GlavniView.xaml">')
assert 'Page Include="Themes\\FinanceTheme.xaml"' in text
project.write_text(text,encoding='utf-8-sig')
snapshot(UI/'GlavniView.xaml.cs')
print(RUN)
