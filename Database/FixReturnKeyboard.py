from pathlib import Path
from lxml import etree as E
root=Path(__file__).resolve().parents[1]
path=root/'Materijalno.ViewModel/PovratMaterijalaViewModel.cs';s=path.read_text(encoding='utf-8-sig')
if 'public Mat SaveCurrentReturn()' in s:raise SystemExit('Return keyboard flow already applied.')
def replace_method(signature,new):
    global s
    start=s.index(signature);left=s.index('{',start);depth=1;end=left+1
    while depth:
        if s[end]=='{':depth+=1
        elif s[end]=='}':depth-=1
        end+=1
    s=s[:start]+new+s[end:]
first=s.index('public PovratMaterijalaViewModel(GlavniViewModel gvm)')
a=s.index('#region Commands',first);b=s.index('#endregion',a)+len('#endregion')
commands=s[a+len('#region Commands'):b-len('#endregion')]
s=s[:a]+'InitializeCommands();'+s[b:]
second=s.index('public PovratMaterijalaViewModel(GlavniViewModel gvm, Mat CurrentItemMat)')
a=s.index('#region Commands',second);b=s.index('#endregion',a)+len('#endregion')
s=s[:a]+'InitializeCommands();'+s[b:]
replace_method('public PovratMaterijalaViewModel()', '''public PovratMaterijalaViewModel()
        {
            MatList = new ObservableCollection<Mat>();
            InitializeCommands();
        }

        private void InitializeCommands()
        {'''+commands+'''
        }''')
s=s.replace('public event PropertyChangedEventHandler PropertyChanged;', '''public event PropertyChangedEventHandler PropertyChanged;
        public event Action RequestFocusSlijedecaStavka;
        public event Action RequestFocusIdent;''',1)
replace_method('private void SpasiNovuKalkulaciju()', '''private void SpasiNovuKalkulaciju()
        {
            SaveCurrentReturn();
        }

        public Mat SaveCurrentReturn()
        {
            if (!PovratAktivan || !isNovaKalkulacijaClicked || CurrentItemMat == null)
                throw new InvalidOperationException("Prvo otvorite unos stavke povrata.");
            CurrentItemMat.Status = "V";
            CurrentItemMat.Kontosklad1 = 0;
            if (!ValidacijaSpasi()) throw new InvalidOperationException("Molimo unesite sva polja.");
            var saved = new MaterialPostingService().Save(CurrentItemMat);
            using (var db = new materijalno_knjigovodstvoContext())
            {
                MatList = new ObservableCollection<Mat>(db.Mat.Where(m => m.Status == "V" && m.Kljnaz >= 1001 && m.Kljnaz <= 1012)
                    .OrderBy(m => m.Datun).ThenBy(m => m.Id).ToList());
                CurrentIndex = MatList.ToList().FindIndex(m => m.Id == saved.Id);
                CurrentItemMat = saved;
                isNovaKalkulacijaClicked = false; _isNovaKalkulacijaClicked = false;
                IsOdustaniEnabled = false; isIzlazEnable = true;
                OnPropertyChanged(nameof(MatList)); UpdateCommands();
                UpdateCurrentItemData(db);
            }
            RequestFocusSlijedecaStavka?.Invoke();
            return CurrentItemMat;
        }''')
start=s.index('private void SlijStavkaButton()');end=s.index('private void OtvoriKomitentListu()',start)
part=s[start:end]
part=part.replace('                UpdateCurrentItemData(dbContext);','                UpdateCurrentItemData(dbContext);\n                RequestFocusIdent?.Invoke();')
s=s[:start]+part+s[end:]
path.write_text(s,encoding='utf-8-sig')

path=root/'Materijalno.UI/PovratMaterijala.xaml';tree=E.parse(str(path));p='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';x='{http://schemas.microsoft.com/winfx/2006/xaml}'
for el in tree.iter(p+'DatePicker'):
    value=el.get('SelectedDate','');el.attrib.pop('KeyDown',None)
    el.set('PreviewKeyDown','DatePicker_PreviewKeyDown');el.set('Loaded','DatePicker_Loaded')
    if '.Datun' in value:el.set('SelectedDate','{Binding CurrentItemMat.Datun, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}')
    elif '.Datnar' in value:
        el.set(x+'Name','dtpDatnar');el.set('SelectedDate','{Binding CurrentItemMat.Datnar, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}')
for el in tree.iter(p+'TextBox'):
    value=el.get('Text','')
    if '.Brdok' in value:el.set(x+'Name','txtBrdok')
    elif '.Ident' in value:el.set(x+'Name','txtIdent')
    elif '.Kolic' in value:
        el.set(x+'Name','txtKolicina');el.set('KeyDown','Kolicina_KeyDown')
        el.set('Text',"{Binding CurrentItemMat.Kolic, Mode=TwoWay, UpdateSourceTrigger=LostFocus, StringFormat=N2, TargetNullValue='0,00', ConverterCulture='de-DE'}")
path.write_bytes(E.tostring(tree,encoding='utf-8'))
print('Return focus events and both date editors connected.')
