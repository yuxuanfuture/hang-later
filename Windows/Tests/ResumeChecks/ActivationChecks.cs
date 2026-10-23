using HangLater.Windows.Services;

internal static class ActivationChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var normal = new Fake();
        check(await Run(normal)==WindowActivationStatus.Activated && normal.ShowCalls==0,"Normal windows activate without changing their show state");
        var slow = new Fake { Iconic=true, RestoreAt=18 };
        check(await Run(slow)==WindowActivationStatus.Activated && slow.Ticks>=18,"Slow minimized windows can finish restoring after the old 600ms deadline");
        var command = new Fake { Iconic=true, RestoreAt=int.MaxValue, RestoreViaCommand=true };
        check(await Run(command)==WindowActivationStatus.Activated && command.CommandCalls==1,"Custom window procedures receive one standard system-restore fallback");
        var blocked = new Fake { Iconic=true, RestoreAt=int.MaxValue, ShowAllowed=false, CommandAllowed=false };
        check(await Run(blocked)==WindowActivationStatus.RestoreRejected && blocked.CommandCalls==1,"Rejected restoration is reported without pretending the app is missing");
        var hung = new Fake { Iconic=true, RestoreAt=int.MaxValue };
        check(await Run(hung)==WindowActivationStatus.RestoreTimedOut && hung.Ticks==40,"An unresponsive minimized app has a bounded asynchronous wait");
        var hidden = new Fake { IsVisible=false };
        check(await Run(hidden)==WindowActivationStatus.NotVisible && hidden.ShowCalls==0,"Hidden tray shells are not force-shown or restored to a blank UI");
        var gone = new Fake { IsAlive=false };
        check(await Run(gone)==WindowActivationStatus.TargetGone && gone.ForegroundCalls==0,"Destroyed windows are not activated");
        var disappearing = new Fake { AfterDelay=p=>p.IsAlive=false };
        check(await Run(disappearing)==WindowActivationStatus.TargetGone,"A window destroyed during activation is reported safely");
        var modal = new Fake { Popup=2, PopupOwner=1 };
        check(await Run(modal)==WindowActivationStatus.Activated && modal.LastRequested==2,"A visible enabled owned popup receives foreground instead of its blocked owner");
        var unrelated = new Fake { Popup=2, PopupOwner=3 };
        check(await Run(unrelated)==WindowActivationStatus.Activated && unrelated.LastRequested==1,"An unrelated popup is never activated by association alone");
        var invisiblePopup = new Fake { Popup=2, PopupOwner=1, PopupVisible=false };
        check(await Run(invisiblePopup)==WindowActivationStatus.Activated && invisiblePopup.LastRequested==1,"Hidden utility popups do not hijack activation");
        var disabledPopup = new Fake { Popup=2, PopupOwner=1, PopupEnabled=false };
        check(await Run(disabledPopup)==WindowActivationStatus.Activated && disabledPopup.LastRequested==1,"Disabled popups do not hijack activation");
        var denied = new Fake { ForegroundAllowed=false };
        check(await Run(denied)==WindowActivationStatus.ForegroundDenied && denied.Ticks==40,"Foreground denial is distinct from restoration or installation failure");
        var falseSuccess = new Fake { ForegroundAllowed=false, ForegroundReturn=true };
        check(await Run(falseSuccess)==WindowActivationStatus.ForegroundDenied,"A successful API return alone does not prove actual foreground transfer");
        var delayedForeground = new Fake { ForegroundAllowed=false, AfterDelay=p=>{if(p.Ticks==15)p.Front=1;} };
        check(await Run(delayedForeground)==WindowActivationStatus.Activated,"Foreground transitions are verified after asynchronous completion");
    }

    private static Task<WindowActivationStatus> Run(Fake platform) => WindowActivationService.ActivateAsync(1,platform);

    private sealed class Fake : WindowActivationService.IPlatform
    {
        public bool IsAlive=true,IsVisible=true,Iconic,ShowAllowed=true,CommandAllowed=true,RestoreViaCommand;
        public bool ForegroundAllowed=true,ForegroundReturn=true,PopupVisible=true,PopupEnabled=true;
        public int Ticks,RestoreAt=1,ShowCalls,CommandCalls,ForegroundCalls;
        public nint Front=99,Popup=1,PopupOwner=1,LastRequested;
        public Action<Fake>? AfterDelay;
        public bool Exists(nint window)=>IsAlive;
        public bool Visible(nint window)=>window==1?IsVisible:PopupVisible;
        public bool Minimized(nint window)=>Iconic;
        public bool Enabled(nint window)=>window==1||PopupEnabled;
        public nint RootOwner(nint window)=>window==Popup?PopupOwner:window;
        public nint LastPopup(nint window)=>Popup;
        public bool Restore(nint window){ShowCalls++;return ShowAllowed;}
        public bool RestoreSystemCommand(nint window){CommandCalls++;if(CommandAllowed&&RestoreViaCommand)Iconic=false;return CommandAllowed;}
        public bool Foreground(nint window){ForegroundCalls++;LastRequested=window;if(ForegroundAllowed&&!Iconic)Front=window;return ForegroundReturn;}
        public nint CurrentForeground()=>Front;
        public Task Delay(){Ticks++;if(ShowAllowed&&Ticks>=RestoreAt)Iconic=false;AfterDelay?.Invoke(this);return Task.CompletedTask;}
    }
}
