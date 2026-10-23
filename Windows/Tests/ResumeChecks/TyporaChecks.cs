using System.IO;
using System.Text;
using System.Text.Json;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

internal static class TyporaChecks
{
    public static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"HangLater-TyporaChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string note=Path.Combine(root,"中文 # & 100%.md"),history=Path.Combine(root,"history.data");
        File.WriteAllText(note,"isolated fixture");
        void History(params string[] paths) => File.WriteAllText(history,JsonSerializer.Serialize(new { recentDocument=paths.Select(p=>new {path=p}) }));
        string title=Path.GetFileName(note)+" - Typora";
        History(note);
        check(TyporaResumeService.TryResolve("Typora",title,history)==note,"Typora resolves a unique document from path metadata, not document contents");
        File.WriteAllText(history,Convert.ToHexString(Encoding.UTF8.GetBytes(File.ReadAllText(history))));
        check(TyporaResumeService.TryResolve("Typora",title,history)==note,"Typora hexadecimal UTF-8 history is decoded including Chinese filenames");
        check(TyporaResumeService.TryResolve("Other",title,history)==null,"Other applications never consume Typora history");
        check(TyporaResumeService.TryResolve("Typora","Untitled - Typora",history)==null,"An unsaved Typora window never becomes the last recently opened document");
        check(TyporaResumeService.TryResolve("Typora","Other.md - Typora",history)==null,"Unmatched titles never fall back to the newest recent file");
        check(TyporaResumeService.TryResolve("Typora",note+" - Typora",history)==note,"A full document path in the title is authoritative");
        History(note,note.ToUpperInvariant());
        check(TyporaResumeService.TryResolve("Typora",title,history)==note,"Repeated case-only Windows history entries are deduplicated");
        string second=Path.Combine(root,"Other",Path.GetFileName(note));Directory.CreateDirectory(Path.GetDirectoryName(second)!);File.WriteAllText(second,"second fixture");
        History(note,second);
        check(TyporaResumeService.TryResolve("Typora",title,history)==null,"Same-named documents in different folders remain ambiguous regardless of recency");
        History(note,Path.Combine(root,"Deleted",Path.GetFileName(note)));
        check(TyporaResumeService.TryResolve("Typora",title,history)==null,"A missing historical namesake cannot redirect a receipt to another file");
        check(TyporaResumeService.TryResolve("Typora",second+" - Typora",history)==second,"An exact full title path disambiguates namesakes");
        History(note);
        check(TyporaResumeService.TryResolve("Typora","*"+title,history)==note,"Typora's unsaved-change marker does not change the document identity");
        File.WriteAllText(history,"broken hex");
        check(TyporaResumeService.TryResolve("Typora",title,history)==null,"A corrupt or transient Typora history file fails safely");
        File.WriteAllText(history,"{\"recentDocument\":5}");
        check(TyporaResumeService.TryResolve("Typora",title,history)==null,"Unexpected history shapes do not crash resume");
        check(TyporaResumeService.ExistingDocument("relative.md")==null && TyporaResumeService.ExistingDocument(@"\\server\share\note.md")==null,"Relative and remote document paths are rejected without disk searches or network access");
        string program=Path.Combine(root,"Typora.exe");File.WriteAllBytes(program,new byte[]{0});
        var task=new HangingTask {appName="Typora",appBundleIdentifier=program,title=title,capturedDocumentPath=note};
        var start=TyporaResumeService.StartInfo(task);
        check(start!=null && start.FileName==program && start.ArgumentList.Count==1 && start.ArgumentList[0]==note,"The specific document is passed as one argument to Typora, not the default Markdown editor");
        check(start?.WorkingDirectory==root && start.UseShellExecute,"Typora document launch retains the validated application working directory");
        task.title="A renamed receipt";task.capturedWindowTitle=title;
        check(TyporaResumeService.StartInfo(task)?.ArgumentList.Single()==note,"Editing a receipt title does not change its saved document");
        task.capturedDocumentPath=Path.Combine(root,"Missing.md");bool missing=false;
        try { TyporaResumeService.StartInfo(task); } catch(FileNotFoundException) {missing=true;}
        check(missing,"Deleted saved documents report failure instead of opening or creating another file");
        task.capturedDocumentPath=null;History(note);
        check(TyporaResumeService.StartInfo(task,history)?.ArgumentList.Single()==note,"Legacy Typora receipts recover only the unique original-title path");
        History(note,second);bool ambiguous=false;
        try { TyporaResumeService.StartInfo(task,history); } catch(InvalidOperationException) {ambiguous=true;}
        check(ambiguous,"An ambiguous legacy receipt requests a path instead of silently switching to the wrong document");
        var repo=new TaskRepository(Path.Combine(root,"Repository"));var stored=repo.Add(title,appName:"Typora");stored.capturedDocumentPath=note;repo.Update(stored);
        check(new TaskRepository(repo.RootDirectory).Tasks.Single().capturedDocumentPath==note,"Captured document identity survives task repository serialization");
    }
}
