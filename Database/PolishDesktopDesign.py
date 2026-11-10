"""Finish theme aliases and compact action labels in all desktop screens."""
from pathlib import Path
from lxml import etree as E
import copy,re
ROOT=Path(__file__).resolve().parents[1];UI=ROOT/'Materijalno.UI'
P='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';X='{http://schemas.microsoft.com/winfx/2006/xaml}'
palette={'#F4F7FB':'#F1F5F7','#2563EB':'#087F82','#1D4ED8':'#06686C','#1E40AF':'#07575C','#EEF5FF':'#E7F3F2','#1F2937':'#203447','#667085':'#596D7E','#E4E7EC':'#D7E1E7','#D8E0EA':'#CDD8E0','#D5DFEC':'#CDD8E0','#F2F6FC':'#EEF7F6','#E5EEFB':'#D8EFEB','#DCE9FF':'#D8EFEB','#E8EEF7':'#DFEEEF'}
aliases={'FormLabelStyle':'FinanceLabel','FormTextBoxStyle':'FinanceTextBox','FormDatePickerStyle':'FinanceDatePicker','PrimaryButtonStyle':'FinancePrimaryButton','SecondaryButtonStyle':'FinanceButton','PrimaryActionButtonStyle':'FinancePrimaryButton','ActionButtonStyle':'FinanceButton','DeleteActionButtonStyle':'FinanceDangerButton','TableButtonStyle':'FinanceButton','TableIconButtonStyle':'FinanceButton'}
numeric=re.compile(r'\.(?:Kolic\w*|Nc|Vrijed\w*|Fvrijed|Porppp|Trospe|Cartro|Zavtro|Ppp)(?:[,}\s]|$)',re.I)
for path in UI.glob('*.xaml'):
    if path.name=='App.xaml':continue
    tree=E.parse(str(path));root=tree.getroot()
    if root.tag!=P+'UserControl':continue
    if path.name=='GlavniView.xaml':
        for tb in root.iter(P+'TextBlock'):
            if tb.get('Text')=='Sve za vaš radni dan.':tb.set('Text','Materijalno knjigovodstvo')
        path.write_bytes(E.tostring(tree,encoding='utf-8'));continue
    for el in list(root.iter()):
        if not isinstance(el.tag,str):continue
        for key,val in list(el.attrib.items()):
            if val.upper() in palette:el.set(key,palette[val.upper()])
        if el.tag==P+'Style':
            key=el.get(X+'Key');target=el.get('TargetType','').replace('{x:Type ','').replace('}','')
            base=aliases.get(key)
            if not base and target in ('GridViewColumnHeader','ListViewItem'):base='FinanceColumnHeader' if target=='GridViewColumnHeader' else 'FinanceListRow'
            if base:
                dims=[copy.deepcopy(c) for c in el if c.tag==P+'Setter' and c.get('Property') in ('Width','Height','MinWidth','Margin')]
                for child in list(el):el.remove(child)
                el.set('BasedOn','{StaticResource '+base+'}')
                for dim in dims:
                    if dim.get('Property')=='Height' and float(dim.get('Value','0'))>48:dim.set('Value','40')
                    el.append(dim)
            elif target in ('TextBox','DatePicker','Button','ListView') and not el.get('BasedOn'):el.set('BasedOn','{StaticResource Finance'+target+'}')
        if el.tag in (P+'TextBox',P+'Button',P+'Label') and el.get('FontSize') in ('16','18'):el.set('FontSize','14' if el.tag==P+'TextBox' else '13')
        if el.tag==P+'TextBox' and numeric.search(el.get('Text','')):el.set('TextAlignment','Right');el.set('Typography.NumeralAlignment','Tabular')
        if el.tag==P+'TextBlock' and el.get('FontSize')=='22':el.set('Foreground','#203447');el.set('FontWeight','SemiBold')
        if el.tag==P+'ListView':el.set('AlternationCount','2');el.set('FontSize','13')
        if el.tag==P+'GridViewColumn' and numeric.search(el.get('DisplayMemberBinding','')):
            binding=el.attrib.pop('DisplayMemberBinding');ct=E.SubElement(el,P+'GridViewColumn.CellTemplate');dt=E.SubElement(ct,P+'DataTemplate');E.SubElement(dt,P+'TextBlock',Text=binding,HorizontalAlignment='Stretch',TextAlignment='Right',Margin='10,0',**{'Typography.NumeralAlignment':'Tabular'})
        if el.tag==P+'Button':
            command=el.get('Command','');image=el.find(P+'Image');caption=None
            if image is not None:
                if 'Izmjena' in command:caption='Uredi'
                elif 'Delete' in command or 'Obrisi' in command:caption='Obriši'
                elif 'UnosSifarnik' in command or 'OpenSifarnik' in command:caption='Dodaj'
                elif 'StampaCommand' in command or el.get('Click')=='stampa_button':caption='Štampa'
                if caption:el.remove(image);el.set('Content',caption);el.set('Width','90');el.set('Height','38')
            if el.get('Style')=='{StaticResource TableIconButtonStyle}':el.set('Width','46');el.set('Height','28');el.set('MinHeight','28');el.set('Padding','3,3');el.set('FontSize','11')
            if not el.get('Style') and el.find(P+'Button.Style') is None:
                content=el.get('Content','').lower()
                if content in ('snimi','spasi','sačuvaj','formiraj','štampa','pregled i štampa'):el.set('Style','{StaticResource FinancePrimaryButton}')
                elif content in ('brisanje','obriši'):el.set('Style','{StaticResource FinanceDangerButton}')
    if path.stem in ('UlazMaterijala','IzlazMaterijala','PovratMaterijala','Medjuskladisnica','UnosInventurnogStanja','PocetnoStanjePrviPut'):
        for toolbar in root.iter(P+'WrapPanel'):
            if not toolbar.findall(P+'Button'):continue
            for button in toolbar.findall(P+'Button'):
                button.set('Height','38');button.set('FontSize','13')
                if button.get('Content') in ('>','<'):
                    button.set('MinWidth','40');button.set('Width','40');button.set('FontSize','18');button.set('Padding','8,3')
                elif button.get('Content') in ('Prvi','Zadnji'):button.set('MinWidth','64')
    path.write_bytes(E.tostring(tree,encoding='utf-8'))
print('Shared theme applied throughout every control resource dictionary.')
