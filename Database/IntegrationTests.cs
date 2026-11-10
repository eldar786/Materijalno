using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Materijalno.ViewModel;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;
using Microsoft.EntityFrameworkCore;

public static class IntegrationTests
{
    static int passed;
    static readonly DateTime Day = new DateTime(2026, 11, 4);
    static MaterialPostingService posting = new MaterialPostingService();
    static InventoryService inventory = new InventoryService();
    static FinancialPostingService finance = new FinancialPostingService();
    static void Check(string name, bool condition) { if (!condition) throw new Exception("FAILED: " + name); passed++; Console.WriteLine("PASS " + name); }
    static void Reject(string name, Action operation) { bool rejected = false; try { operation(); } catch (Exception e) when (e is InvalidOperationException || e is DbUpdateException) { rejected = true; } Check(name, rejected); }
    static materijalno_knjigovodstvoContext Db() => new materijalno_knjigovodstvoContext();
    static Mat Row(string status, string number, int material, int warehouse, decimal quantity) => new Mat {
        Status = status, Brfak = number, Brdok = "INTEGRACIJSKI TEST", Ident = material, Kljnaz = warehouse,
        Kolic = quantity, Datun = Day, Datnar = Day, Redbr = 1, Fvrijed = "105.00", Brnar = "RN-TEST", Analst = "TEST" };
    static Mat Fresh(int id) { using (var db = Db()) return db.Mat.AsNoTracking().Single(m => m.Id == id); }
    sealed class Gateway : IFinancialGateway {
        public bool Remote, LoseAcknowledgement, FailBeforeCommit; public int Sends;
        public RemoteJournalState Inspect(FinancialBatch b, IList<Nalmat> l) => Remote ? RemoteJournalState.Matches : RemoteJournalState.Absent;
        public void SendAtomically(FinancialBatch b, IList<Nalmat> l) {
            Sends++; if (FailBeforeCommit) throw new InvalidOperationException("test rollback");
            Remote = true; if (LoseAcknowledgement) { LoseAcknowledgement = false; throw new InvalidOperationException("test lost acknowledgement"); }
        }
    }
    [STAThread]
    public static int Main()
    {
        try {
            string catalog = new SqlConnectionStringBuilder(DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
            if (!catalog.StartsWith("materijalno_fox_test_", StringComparison.Ordinal)) throw new Exception("Tests require isolated test catalog.");
            Console.WriteLine("TEST DATABASE " + catalog);
            using (var db = Db()) db.Database.ExecuteSqlRaw("DELETE FROM dbo.financial_export_log; DELETE FROM dbo.nalmat WHERE fox_recno IS NULL; DELETE FROM dbo.financial_batch; DELETE FROM dbo.inv WHERE fox_recno IS NULL; DELETE FROM dbo.mat WHERE fox_recno IS NULL; DELETE FROM dbo.sifarnik_materijal_skladiste_konto WHERE Sifmat>=990001; DELETE FROM dbo.tabela_materijala WHERE ident>=990001; DELETE FROM dbo.material_document WHERE NOT EXISTS(SELECT 1 FROM dbo.mat WHERE document_id=material_document.id); DELETE FROM dbo.year_close; UPDATE dbo.mat SET is_archived=0; UPDATE dbo.inv SET is_archived=0; UPDATE dbo.app_settings SET value='2026' WHERE key_name='ActiveBookYear';");
            int w1, w2, account, expense;
            using (var db = Db()) {
                Check("Fox operational and raw filters", db.Mat.Count() == 3473 && db.Mat.IgnoreQueryFilters().Count() == 3482 && db.Nalmat.Count() == 4888);
                Check("known Fox return retains positive quantity/value", db.Mat.Any(m => m.Brfak == "61-209" && m.Status == "V" && m.Ident == 157 && m.Kljnaz == 1001 && m.Kolic == 10966 && m.Vrijed == 802.63m));
                var main = new GlavniViewModel();
                foreach (var vmType in new[] { typeof(UlazMaterijalaViewModel), typeof(IzlazMaterijalaViewModel), typeof(PovratMaterijalaViewModel), typeof(MedjuskladisnicaViewModel), typeof(PocetnoStanjePrviPutViewModel) })
                {
                    var vm = Activator.CreateInstance(vmType, new object[] { main });
                    string command = vmType == typeof(MedjuskladisnicaViewModel) ? "NovaMedjuskladisnica" : vmType == typeof(PovratMaterijalaViewModel) ? "NoviPovrat" : "NovaKalkulacija";
                    vmType.GetMethod(command, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vm, null);
                    var draft = (Mat)vmType.GetProperty("CurrentItemMat").GetValue(vm);
                    Check(vmType.Name + " creates a valid excluded draft", draft.IsDraft && draft.Id > 0 && !db.Mat.Any(m => m.Id == draft.Id));
                    posting.Delete(draft);
                }
                var inventoryVm = new UnosInventurnogStanjaViewModel(main);
                typeof(UnosInventurnogStanjaViewModel).GetMethod("NovaKalkulacija",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(inventoryVm,null);
                Check("empty inventory screen creates draft without material movement", inventoryVm.CurrentItemInv.IsDraft && inventoryVm.CurrentItemInv.Id>0 && !db.Inv.Any());
                inventory.Delete(inventoryVm.CurrentItemInv);
                var links = db.SifarnikMaterijalSkladisteKonto.OrderBy(m => m.Id).ToList();
                w1 = links[0].Sifskla.Value; w2 = links.First(l => l.Sifskla != w1).Sifskla.Value; account = links[0].Sifkonta.Value;
                expense = db.TabelaMaterijala.First(m => m.Konto1 != null).Konto1.Value;
                for (int i = 990001; i <= 990004; i++) db.SifarnikMaterijala.Add(new SifarnikMaterijala { Ident = i, Nazmat = "INTEGRACIJSKI TEST ČĆŽŠĐ " + new string('x', 120), Jedm = "kom", Siftar = 1, Konto1 = expense, Konto2 = "A-01" });
                db.SaveChanges();
                Check("master view writes canonical table and preserves long Unicode name", db.TabelaMaterijala.Count(m => m.Ident >= 990001) == 4 && db.TabelaMaterijala.Single(m => m.Ident == 990001).Nazmat.Contains("ČĆŽŠĐ"));
                for (int i = 990001; i <= 990004; i++) foreach (int warehouse in new[] {w1,w2})
                    db.SifarnikMaterijalSkladisteKonto.Add(new SifarnikMaterijalSkladisteKonto { Sifmat = i, Sifskla = warehouse, Sifkonta = account });
                db.SaveChanges();
            }
            var u = posting.Save(Row("U", "10-TEST1", 990001, w1, 10.5m));
            Check("receipt decimal quantity / nine decimal price / rounded money", u.Kolic == 10.5m && u.Nc == 10m && u.Vrijed == 105m);
            var i1 = posting.Save(Row("I", "61-TEST1", 990001, w1, 2.25m));
            Check("issue signs and weighted cost", i1.Kolic == -2.25m && i1.Vrijed == -22.50m && posting.Balance(990001,w1).Quantity == 8.25m);
            posting.Save(i1);
            using (var db=Db()) Check("repeated save is idempotent", db.Mat.Count(m=>m.LineToken==i1.LineToken)==1);
            Reject("issue greater than stock rejected",()=>posting.Save(Row("I","61-OVER",990001,w1,8.26m)));
            var transfer=Row("M","M-TEST1",990001,w1,3.25m); transfer.Kljnaz1=w2;
            var destination=posting.SaveTransfer(transfer);
            var transferVm = new MedjuskladisnicaViewModel(new GlavniViewModel()) { CurrentItemMat = destination };
            Check("transfer screen labels keep source and destination orientation", transferVm.SourceWarehouse==w1 && transferVm.DestinationWarehouse==w2);
            using (var db=Db()) { var pair=db.Mat.Where(m=>m.TransferPairId==destination.TransferPairId).ToList(); Check("transfer creates two opposite complete rows atomically",pair.Count==2 && pair.Sum(m=>m.Kolic)==0 && pair.Sum(m=>m.Vrijed)==0 && posting.Balance(990001,w1).Quantity==5); }
            destination.Kolic=4; destination=posting.SaveTransfer(destination);
            using(var db=Db()) Check("transfer edit updates both sides",db.Mat.Where(m=>m.TransferPairId==destination.TransferPairId).Sum(m=>Math.Abs(m.Kolic.Value))==8 && posting.Balance(990001,w1).Quantity==4.25m);
            posting.Delete(destination);
            Check("transfer delete restores both warehouses",posting.Balance(990001,w1).Quantity==8.25m && posting.Balance(990001,w2).Quantity==0);
            var u2=Row("U","10-TEST2",990001,w2,1);u2.Fvrijed="7.33";u2=posting.Save(u2);
            Check("return price comes from selected warehouse",posting.ReturnPrice(990001,w2)==7.33m && posting.ReturnPrice(990001,w1)==10m);
            var v=posting.Save(Row("V","61-TESTV",990001,w2,0.5m));Check("return uses latest same warehouse price and money rounding",v.Kolic==0.5m && v.Vrijed==3.67m);
            var full=posting.Save(Row("I","61-EMPTY",990001,w2,1.5m));Check("full issue removes rounding residue",posting.Balance(990001,w2).Quantity==0 && posting.Balance(990001,w2).Value==0);
            var inv=new Inv{Kljnaz=w2,Ident=990001,Kolic=0,Datun=Day};inv=inventory.Save(inv);
            Check("inventory handles zero stock and preserves date",inv.Nc==full.Nc && inv.Vrijed==0 && inv.InventoryDate==Day);
            Reject("duplicate inventory line rejected",()=>inventory.Save(new Inv{Kljnaz=w2,Ident=990001,Kolic=0,Datun=Day}));
            var past=new Inv{Kljnaz=w1,Ident=990001,Kolic=0,Datun=new DateTime(2026,1,31)};
            Check("inventory quotation respects historical cutoff",inventory.Quote(past)==0);
            var parallel = new[] {"61-CON1","61-CON2"}.Select(n=>Task.Run(()=>{try{posting.Save(Row("I",n,990001,w1,6));return true;}catch(InvalidOperationException){return false;}})).ToArray();
            Task.WaitAll(parallel); Check("concurrent issues cannot overspend stock",parallel.Count(t=>t.Result)==1 && posting.Balance(990001,w1).Quantity==2.25m);
            var p=Row("P","",990002,w1,4.5m);p.Nc=2m;p=posting.Save(p);
            Check("opening balance uses active year January 1",p.Datun==new DateTime(2026,1,1)&&p.Vrijed==9m);
            var duplicate=Row("P","",990002,w1,1);duplicate.Nc=2m;Reject("duplicate opening balance rejected",()=>posting.Save(duplicate));
            var old=Fresh(p.Id);p.Brdok="NEW";p=posting.Save(p);old.Brdok="STALE";Reject("stale material edit rejected by row version",()=>posting.Save(old));
            var m=Row("M","M-FINTEST",990002,w1,1.25m);m.Kljnaz1=w2;m=posting.SaveTransfer(m);
            foreach(var pair in new[]{Tuple.Create("U",u.Brfak),Tuple.Create("I",i1.Brfak),Tuple.Create("V",v.Brfak),Tuple.Create("M",m.Brfak)}) {
                var batch=finance.Prepare(pair.Item1,pair.Item2);var again=finance.Prepare(pair.Item1,pair.Item2);
                using(var db=Db()) {var entries=db.Nalmat.Where(n=>n.BatchId==batch.Id).ToList();Check(pair.Item1+" finance balanced, sequenced and idempotent",entries.Count>=2&&entries.Sum(n=>n.Dug1st)==entries.Sum(n=>n.Pot1st)&&entries.Select(n=>n.LineNumber).Distinct().Count()==entries.Count&&again.Id==batch.Id);if(pair.Item1=="V")Check("return journal preserves signed storno on both sides",entries.Sum(n=>n.Dug1st)<0&&entries.Sum(n=>n.Pot1st)<0);if(pair.Item1=="M")Check("transfer journal debit and credit positive",entries.Sum(n=>n.Dug1st)>0&&entries.Sum(n=>n.Pot1st)>0);}
            }
            var prepared=finance.Prepare("U",u.Brfak);var remote=new Gateway{LoseAcknowledgement=true};
            Reject("export lost acknowledgement persists failure",()=>finance.Export(prepared.Id,remote));
            var recovered=finance.Export(prepared.Id,remote);finance.Export(prepared.Id,remote);
            Check("export retry recognizes remote commit without duplicate send",recovered.State=="Sent"&&remote.Sends==1);
            var pending=finance.Prepare("I",i1.Brfak);var failed=new Gateway{FailBeforeCommit=true};Reject("export failure is retryable",()=>finance.Export(pending.Id,failed));failed.FailBeforeCommit=false;
            Check("retry after precommit failure succeeds",finance.Export(pending.Id,failed).State=="Sent"&&failed.Sends==2);
            Reject("formed journal locks source material document",()=>posting.Delete(u));
            using(var db=Db()) {var existing=db.SifarnikMaterijala.Single(s=>s.Ident==990001);db.SifarnikMaterijala.Remove(existing);Reject("referenced material deletion rejected",()=>db.SaveChanges());}
            using(var db=Db()) {var row=db.Mat.First();row.Vrijed+=1;Reject("direct posted movement writes rejected",()=>db.SaveChanges());}
            Check("summary report is populated and includes signed returns",SummaryReports.Create(SummaryReportKind.Costs,new DateTime(2026,1,1),Day,w2,null).Rows.Rows.Count>0);
            int before;using(var db=Db())before=db.Mat.IgnoreQueryFilters().Count();
            Reject("closing current calendar year rejected",()=>new YearCloseService(today:()=>Day).CloseFromInventory());
            Reject("incomplete closing inventory rejected",()=>new YearCloseService(today:()=>new DateTime(2027,1,2)).CloseFromInventory());
            using(var db=Db())Check("failed year close leaves records and active year untouched",db.Mat.IgnoreQueryFilters().Count()==before&&posting.ActiveYear()==2026&&db.YearCloses.Count()==0);
            // A small active fixture lets the closing transaction be tested while retaining every imported row.
            using (var db=Db()) db.Database.ExecuteSqlRaw("UPDATE dbo.mat SET is_archived=1 WHERE fox_recno IS NOT NULL; DELETE FROM dbo.inv WHERE fox_recno IS NULL;");
            using (var db=Db()) foreach(var batch in db.FinancialBatches.Where(b=>b.State!="Sent").Select(b=>b.Id).ToList()) finance.Export(batch,new Gateway());
            using (var db=Db()) {
                var balances=db.Mat.GroupBy(r=>new{r.Ident,r.Kljnaz}).Select(g=>new{g.Key.Ident,g.Key.Kljnaz,Quantity=g.Sum(r=>r.Kolic)??0,Value=g.Sum(r=>r.Vrijed)??0}).ToList();
                foreach(var balance in balances.Where(b=>b.Quantity!=0||b.Value!=0)) inventory.Save(new Inv{Kljnaz=balance.Kljnaz.Value,Ident=balance.Ident,Kolic=balance.Quantity,Datun=new DateTime(2026,12,31)});
            }
            int beforeClose, nalBefore;decimal openingQuantity,openingValue;
            using(var db=Db()){beforeClose=db.Mat.IgnoreQueryFilters().Count();nalBefore=db.Nalmat.Count();openingQuantity=db.Inv.Sum(r=>r.Kolic)??0;openingValue=db.Inv.Sum(r=>r.Vrijed);db.Database.ExecuteSqlRaw("CREATE TRIGGER dbo.test_close_failure ON dbo.app_settings AFTER UPDATE AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE key_name='ActiveBookYear' AND value='2027') THROW 51050,'Injected close failure',1; END");}
            bool rolledBack=false;try{new YearCloseService(today:()=>new DateTime(2027,1,2)).CloseFromInventory();}catch(DbUpdateException){rolledBack=true;}finally{using(var db=Db())db.Database.ExecuteSqlRaw("DROP TRIGGER dbo.test_close_failure");}
            using(var db=Db())Check("year close failure rolls back archives, openings and year setting",rolledBack&&posting.ActiveYear()==2026&&db.Mat.IgnoreQueryFilters().Count()==beforeClose&&!db.Mat.IgnoreQueryFilters().Any(r=>r.BookYear==2027));
            Check("year close creates next year once",new YearCloseService(today:()=>new DateTime(2027,1,2)).CloseFromInventory()==2027);
            using(var db=Db())Check("year close preserves all history and balances opening",db.Mat.IgnoreQueryFilters().Count(r=>r.FoxRecno!=null)==3482&&db.Nalmat.Count()==nalBefore&&db.Mat.All(r=>r.BookYear==2027&&r.Status=="P")&&db.Mat.Sum(r=>r.Kolic)==openingQuantity&&db.Mat.Sum(r=>r.Vrijed)==openingValue);
            Console.WriteLine("ALL PASSED "+passed);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().FullName+": "+e.Message+"\n"+e.StackTrace);return 1;}
    }
}
