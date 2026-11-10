from pathlib import Path
from lxml import etree as E

ROOT=Path(__file__).resolve().parents[1]; UI=ROOT/'Materijalno.UI'
P='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'; X='{http://schemas.microsoft.com/winfx/2006/xaml}'
parser=E.XMLParser(remove_blank_text=False)
def write(p,t):p.write_bytes(E.tostring(t,encoding='utf-8',pretty_print=False))
def clear_layout(el):
    for key in ('Grid.Row','Grid.Column','Grid.RowSpan','Grid.ColumnSpan','Margin','Width','Height','HorizontalAlignment','VerticalAlignment'):
        el.attrib.pop(key,None)

titles={'SifarnikKontaForm':'Šifarnik konta','SifarnikMaterijalaForm':'Šifarnik materijala',
        'SifarnikSkladistaForm':'Šifarnik skladišta','SifarnikMaterijalSkladisteKontoForm':'Šifarnik materijal-skladište-konta'}
labels={'Ident':'Šifra materijala','Nazmat':'Naziv materijala','Jedm':'Jedinica mjere','Konto1':'Konto 1','Konto2':'Konto 2',
        'Sifkonta':'Šifra konta','Nazkont':'Naziv konta','Kljnaz':'Šifra skladišta','NazivOrg':'Naziv skladišta',
        'Sifmat':'Šifra materijala','Sifskla':'Šifra skladišta'}
for name,title in titles.items():
    path=UI/(name+'.xaml'); tree=E.parse(str(path),parser);root=tree.getroot()
    fields=root.findall('.//'+P+'TextBox'); buttons=root.findall('.//'+P+'Button')
    errors=[e for e in root.findall('.//'+P+'TextBlock') if 'Validation.Errors' in e.get('Text','')]
    resources=root.find(P+'UserControl.Resources')
    for style in resources.iter(P+'Style'):
        if style.get('TargetType')=='TextBlock':
            for c in list(style): style.remove(c)
            style.set('BasedOn','{StaticResource FinanceLabel}')
        elif style.get('TargetType')=='TextBox':
            for c in list(style): style.remove(c)
            style.set('BasedOn','{StaticResource FinanceTextBox}')
    for c in list(root):
        if c!=resources: root.remove(c)
    scroll=E.SubElement(root,P+'ScrollViewer',VerticalScrollBarVisibility='Auto',HorizontalScrollBarVisibility='Disabled',Background='#F1F5F7')
    panel=E.SubElement(scroll,P+'StackPanel',Margin='28,24')
    E.SubElement(panel,P+'TextBlock',Text=title,FontSize='22',FontWeight='SemiBold',Margin='0,0,0,8')
    E.SubElement(panel,P+'TextBlock',Text='Unos i uređivanje podataka',Foreground='#596D7E',FontSize='13',Margin='0,0,0,24',FontWeight='Normal')
    border=E.SubElement(panel,P+'Border',Padding='24',Background='White',BorderBrush='#D7E1E7',BorderThickness='1',CornerRadius='10',MaxWidth='680',HorizontalAlignment='Left')
    form=E.SubElement(border,P+'StackPanel',Width='580')
    for field in fields:
        binding=field.find('.//'+P+'Binding'); prop=binding.get('Path').split('.')[-1]
        clear_layout(field);field.set('Height','38');field.set('FontSize','14');field.set('HorizontalAlignment','Stretch')
        field.set('VerticalAlignment','Center')
        row=E.SubElement(form,P+'Grid',Margin='0,0,0,16')
        defs=E.SubElement(row,P+'Grid.ColumnDefinitions');E.SubElement(defs,P+'ColumnDefinition',Width='150');E.SubElement(defs,P+'ColumnDefinition',Width='*')
        label=E.SubElement(row,P+'Label',Content=labels.get(prop,prop),Padding='0,5,12,5',FontSize='13',VerticalAlignment='Center')
        # The label gives screen readers the same field relationship as the visual layout.
        label.set('Target','{Binding ElementName='+field.get(X+'Name')+'}')
        cell=E.SubElement(row,P+'StackPanel',**{'Grid.Column':'1'});cell.append(field)
        for error in errors:
            if 'ElementName='+field.get(X+'Name')+',' in error.get('Text',''):
                clear_layout(error);error.set('FontSize','11');error.set('Foreground','#A32D3D');error.set('Margin','0,3,0,0');error.set('FontWeight','Normal');error.set('TextWrapping','Wrap');cell.append(error)
    # Keep even pre-existing orphan validation messages rather than drop their binding.
    for error in errors:
        if error.getparent() is not None and error.getroottree().getroot() is not root:
            clear_layout(error);form.append(error)
    toolbar=E.SubElement(form,P+'WrapPanel',Margin='0,10,0,0')
    for button in buttons:
        clear_layout(button);button.set('MinWidth','104');button.set('Height','38');button.set('Margin','0,0,10,0')
        button.attrib.pop('Background',None)
        for image in button.findall(P+'Image'):button.remove(image)
        command=button.get('Command','')
        caption='Sačuvaj' if 'Save' in command else 'Odustani' if 'Cancel' in command else 'Štampa'
        button.set('Content',caption)
        style=button.find(P+'Button.Style')
        base='FinancePrimaryButton' if 'Save' in command else 'FinanceButton'
        if style is not None:style.find(P+'Style').set('BasedOn','{StaticResource '+base+'}')
        else:button.set('Style','{StaticResource '+base+'}')
        toolbar.append(button)
    write(path,tree)

for path in UI.glob('*.xaml'):
    if path.name in ('App.xaml',):continue
    tree=E.parse(str(path),parser);root=tree.getroot()
    if root.tag!=P+'UserControl':continue
    for e in list(root.iter()):
        if not isinstance(e.tag,str):continue
        if e.get('Background')=='AliceBlue':e.set('Background','#F1F5F7')
        if e.tag==P+'Button':
            if e.get(X+'Name')=='arhivaBtn':e.set('ContentTemplate','{x:Null}')
            command=e.get('Command','')
            image=e.find(P+'Image')
            if image is not None and not e.get('Content'):
                caption=None
                if 'UnosSifarnik' in command:caption='Dodaj'
                elif 'Izmjena' in command:caption='Uredi'
                elif 'Obrisi' in command:caption='Obriši'
                if caption:
                    e.remove(image);e.set('Content',caption);e.set('Width','90');e.set('Height','38')
            if e.get('Content')=='Pregled i štampa':e.set('Style','{StaticResource FinancePrimaryButton}')
    # Place transaction fields on a white sheet, with a distinct action row.
    if path.stem in ('UlazMaterijala','IzlazMaterijala','PovratMaterijala','Medjuskladisnica','UnosInventurnogStanja','PocetnoStanjePrviPut'):
        scroll=root.find(P+'ScrollViewer')
        if scroll is not None:
            panel=scroll.find(P+'StackPanel')
            if panel is not None:
                panel.set('Margin','0');scroll.remove(panel)
                card=E.SubElement(scroll,P+'Border',Background='White',BorderBrush='#D7E1E7',BorderThickness='1',CornerRadius='10',Margin='20',Padding='20');card.append(panel)
                wraps=panel.findall(P+'WrapPanel')
                if wraps:
                    actions=wraps[-1];panel.remove(actions);actions.set('Margin','0')
                    line=E.SubElement(panel,P+'Border',Margin='0,14,0,0',Padding='0,18,0,0',BorderBrush='#E1E9ED',BorderThickness='0,1,0,0');line.append(actions)
    write(path,tree)
print('Refined forms, transaction sheets and labelled actions.')
