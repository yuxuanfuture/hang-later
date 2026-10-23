using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using HangLater.Windows.Controls;

namespace HangLater.Windows.Views;

public enum AddSourceChoice { Task, Reminder, Image, Window, Chrome }

public partial class AddSourceWindow : Window
{
    public AddSourceChoice? Selection { get; private set; }

    public AddSourceWindow(App app)
    {
        InitializeComponent();
        SourceDialogLayout.Initialize(this,app);
        Title=app.Text.T("晾一件事","Hang something");
        Heading.Text=app.Text.T("晾一件什么？","WHAT WOULD YOU LIKE TO HANG?");
        Subtitle.Text=app.Text.T("网页、图片、App 窗口，或一件普通待办。","A web page, image, app window, or a regular task.");
        CloseIcon.Source=SourceDialogIcons.Load("close",app.Settings.LightAppearance);
        AutomationProperties.SetName(CloseButton,app.Text.T("关闭","Close"));
        Add(AddSourceChoice.Task,"write-task",app.Text.T("写待办或贴链接","Write a task or paste a link"),app.Text.T("记下一件事，也可以粘贴网页地址。","Write something down or paste a web address."));
        Add(AddSourceChoice.Reminder,"reminder",app.Text.T("挂一个提醒","Hang a reminder"),app.Text.T("选择时间；会议链接和普通链接都可以不填。","Choose a time; a meeting or web link is optional."));
        Add(AddSourceChoice.Image,"choose-image",app.Text.T("选择图片","Choose an image"),app.Text.T("也可以直接把图片拖到晾衣绳上。","You can also drag an image straight onto the clothesline."));
        Add(AddSourceChoice.Window,"capture-app-window",app.Text.T("截取 App 窗口","Capture an app window"),app.Text.T("适用于正在显示的 App；点击纸片可重新唤起它。","Works with any visible app; clicking the card brings it back."));
        Add(AddSourceChoice.Chrome,"chrome-page-video",app.Text.T("从 Chrome 晾网页或视频","Hang a page or video from Chrome"),app.Text.T("视频会自动只保留播放器画面。","Videos automatically keep only the player frame."));

        void Add(AddSourceChoice choice,string icon,string title,string detail)
        {
            var grid=new Grid();
            foreach(var width in new[]{new GridLength(38),new GridLength(15),new GridLength(1,GridUnitType.Star),new GridLength(38),new GridLength(10)})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=width});
            grid.Children.Add(new Border{Width=38,Height=38,Background=(Brush)app.Resources["RaisedBrush"],BorderBrush=(Brush)app.Resources["BorderBrush"],BorderThickness=new Thickness(0.7),Child=new Image{Source=SourceDialogIcons.Load(icon,app.Settings.LightAppearance),Width=26,Height=26,IsHitTestVisible=false}});
            var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            copy.Children.Add(new TextBlock{Text=title,FontFamily=new FontFamily("Arial"),FontStretch=FontStretches.Condensed,FontSize=13,FontWeight=FontWeights.Bold,Foreground=(Brush)app.Resources["InkBrush"],LineHeight=16,TextWrapping=TextWrapping.Wrap});
            copy.Children.Add(new TextBlock{Text=detail,Style=(Style)Resources["DialogCopy"],FontSize=9,LineHeight=11,Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.Wrap});
            Grid.SetColumn(copy,2);grid.Children.Add(copy);
            var arrow=new Image{Source=SourceDialogIcons.Load("chevron-right",app.Settings.LightAppearance),Width=10,Height=10,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(arrow,4);grid.Children.Add(arrow);
            var button=new Button{Name="Choose"+choice,Tag=choice,Style=(Style)Resources["DialogChoiceButton"],Content=grid,Margin=new Thickness(0,SourceOptions.Children.Count==0?0:10,0,0)};
            AutomationProperties.SetName(button,title);button.Click+=(_,_)=>{Selection=choice;DialogResult=true;};SourceOptions.Children.Add(button);
        }
    }
    private void Header_Drag(object sender,MouseButtonEventArgs e)=>SourceDialogLayout.DragHeader(this,e);
    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
