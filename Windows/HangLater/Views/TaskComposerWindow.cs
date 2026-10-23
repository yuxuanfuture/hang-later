using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class TaskComposerWindow : Window
{
    private readonly App app;
    public TaskComposerWindow(App app,bool reminder=false)
    {
        this.app=app;InitializeComponent();SourceDialogLayout.Initialize(this,app);
        Title=app.Text.T("晾一件事","Hang something");
        Heading.Text=reminder?app.Text.T("挂一个提醒。","HANG A REMINDER."):app.Text.T("晾一件事。","HANG SOMETHING.");
        Subtitle.Text=app.Text.T("记下来，给它一个位置。","Write it down and give it a place.");
        TitleLabel.Text=app.Text.T("这件事叫什么","What is it called?");
        LinkLabel.Text=app.Text.T("网页链接（可选）","Web link (optional)");
        NoteLabel.Text=app.Text.T("给未来的自己留句话（可选）","Leave your future self a note (optional)");
        TypeLabel.Text=app.Text.T("任务类型","Task type");
        TitleText.Tag=app.Text.T("例如：继续学习那个视频","For example: continue that course");
        NoteText.Tag=app.Text.T("例如：从 12:30 开始，记一下关键步骤","For example: resume at 12:30 and note the key steps");
        ReminderCheck.Content=app.Text.T("添加提醒时间","Add reminder");
        CancelButton.Content=app.Text.T("取消","Cancel");HangButton.Content=app.Text.T("挂上晾衣绳","Hang it up");
        CloseIcon.Source=SourceDialogIcons.Load("close",app.Settings.LightAppearance);
        AutomationProperties.SetName(CloseButton,app.Text.T("关闭","Close"));
        AutomationProperties.SetName(TitleText,TitleLabel.Text);AutomationProperties.SetName(LinkText,LinkLabel.Text);AutomationProperties.SetName(NoteText,NoteLabel.Text);AutomationProperties.SetName(TypeBox,TypeLabel.Text);
        AutomationProperties.SetName(DueDate,app.Text.T("提醒日期","Reminder date"));AutomationProperties.SetName(DueTime,app.Text.T("提醒时间，HH:mm","Reminder time, HH:mm"));
        var types=new List<TaskTypeDefinition>{new TaskTypeDefinition{id=Guid.Empty,name=app.Text.T("默认边框","Default border")}};
        types.AddRange(app.Repository.TaskTypes.Select(x=>x.Clone()));TypeBox.ItemsSource=types;TypeBox.SelectedIndex=0;
        var defaultTime=DateTime.Now.AddHours(1);DueDate.SelectedDate=defaultTime.Date;DueTime.Text=defaultTime.ToString("HH:mm",CultureInfo.InvariantCulture);
        ReminderCheck.IsChecked=reminder;
        Loaded+=(_,_)=>TitleText.Focus();
    }

    public bool SaveDraft()
    {
        string title=TitleText.Text.Trim();string? link=string.IsNullOrWhiteSpace(LinkText.Text)?null:LinkText.Text.Trim();
        if(title.Length==0)return Fail(app.Text.T("先给这件事起个名字吧。","Give this task a name first."));
        if(link!=null&&!TaskRepository.IsWebUrl(link))return Fail(app.Text.T("请输入完整的 http 或 https 网页链接。","Enter a complete http or https web link."));
        DateTimeOffset? due=null;
        if(ReminderCheck.IsChecked==true)
        {
            if(!DueDate.SelectedDate.HasValue||!TimeSpan.TryParseExact(DueTime.Text.Trim(),new[]{"h\\:mm","hh\\:mm"},CultureInfo.InvariantCulture,out var time))return Fail(app.Text.T("请选择日期，并使用 HH:mm 填写提醒时间。","Choose a date and use HH:mm for the reminder time."));
            due=new DateTimeOffset(DueDate.SelectedDate.Value.Date+time);
            if(due<=DateTimeOffset.Now)return Fail(app.Text.T("请选择未来的提醒时间。","Choose a future reminder time."));
        }
        Guid? typeId=(TypeBox.SelectedItem as TaskTypeDefinition)?.id;
        if(typeId==Guid.Empty)typeId=null;
        if(typeId.HasValue&&!app.Repository.TaskTypes.Any(x=>x.id==typeId))return Fail(app.Text.T("这个任务类型已被移除，请重新选择。","This task type was removed. Choose another type."));
        bool meeting=link!=null&&Uri.TryCreate(link,UriKind.Absolute,out var url)&&new[]{"zoom.us","meeting.tencent.com","feishu.cn","larksuite.com"}.Any(domain=>url.Host.Equals(domain,StringComparison.OrdinalIgnoreCase)||url.Host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase));
        var kind=meeting?HangingTaskKind.meeting:due.HasValue?(HangingTaskKind?)HangingTaskKind.reminder:null;
        try{app.Repository.Add(title,link,NoteText.Text,due,resumeUrl:link,kind:kind,taskTypeID:typeId);ValidationText.Visibility=Visibility.Collapsed;return true;}
        catch(Exception error){return Fail(error.Message);}
    }
    private bool Fail(string text){ValidationText.Text=text;ValidationText.Visibility=Visibility.Visible;ComposerScroll.ScrollToBottom();return false;}
    private void Hang_Click(object sender,RoutedEventArgs e){if(SaveDraft())DialogResult=true;}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
    private void Header_Drag(object sender,MouseButtonEventArgs e)=>SourceDialogLayout.DragHeader(this,e);
    private void Reminder_Changed(object sender,RoutedEventArgs e){if(ReminderTimePanel!=null)ReminderTimePanel.Visibility=ReminderCheck.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
}
