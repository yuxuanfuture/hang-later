// Geometry, receipt layout, camera and gestures ported from Hang Later for macOS:
// Sources/HangLater/ReceiptSpike3D.swift (MIT, copyright 2026 Yuxuan Fu).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HangLater.Windows.Models;

namespace HangLater.Windows.Controls;

public sealed class ReceiptSpike3DView : Grid
{
    public const double MinimumPaperHeight = 0.72;
    public const double MaximumPaperHeight = 3.05;
    private const double PivotHeight = 1.55;
    private readonly Viewport3D viewport = new Viewport3D();
    private readonly Model3DGroup assembly = new Model3DGroup();
    private readonly List<GeometryModel3D> baseModels = new List<GeometryModel3D>();
    private readonly List<Paper> papers = new List<Paper>();
    private readonly AxisAngleRotation3D yawRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D pitchRotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
    private readonly PerspectiveCamera camera = new PerspectiveCamera();
    private GeometryModel3D topModel = null!;
    private Point? lastPointer;
    private Paper? activePaper;
    private Point3D dragPlanePoint;
    private Vector3D dragPlaneNormal;
    private Vector3D grabOffset;
    private double dragDistance;
    private double yaw = 0.42;
    private double pitch = -0.03;
    private double verticalFieldOfView = 35;
    private bool? lightAppearance;

    public event Action<Guid, double, double, double>? ReceiptMoved;
    public event Action<Guid, double>? ReceiptResized;
    public event Action<Guid>? ReceiptClicked;

    // Inside a scrolling page, leave unmodified wheel input to its ScrollViewer.
    public bool UseControlWheelForZoom { get; set; }
    private double initialVerticalFieldOfView = 35;
    public double InitialVerticalFieldOfView
    {
        get => initialVerticalFieldOfView;
        set { initialVerticalFieldOfView = Math.Clamp(value, 29, 44); verticalFieldOfView = initialVerticalFieldOfView; UpdateCamera(); }
    }

    private sealed class Paper
    {
        public HangingTask Task { get; }
        public GeometryModel3D Model { get; }
        public TranslateTransform3D Translation { get; }
        public ScaleTransform3D Scale { get; }
        public double Width { get; }
        public double Depth { get; }
        public Paper(HangingTask task, GeometryModel3D model, TranslateTransform3D translation,
                     ScaleTransform3D scale, double width, double depth)
        {
            Task = task; Model = model; Translation = translation; Scale = scale;
            Width = width; Depth = depth;
        }
    }

    public ReceiptSpike3DView()
    {
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;
        ClipToBounds = true;
        Focusable = true;
        Children.Add(viewport);
        RenderOptions.SetEdgeMode(viewport, EdgeMode.Unspecified);
        BuildScene();
        SizeChanged += (_, _) => UpdateCamera();
        MouseLeftButtonDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseLeftButtonUp += (_, e) => { FinishDrag(); e.Handled = true; };
        LostMouseCapture += (_, _) => FinishDrag();
        MouseWheel += OnZoom;
        KeyDown += OnPaperKey;
    }

    public void Update(IEnumerable<HangingTask> tasks, bool light, bool chinese,
                       IReadOnlyDictionary<Guid, uint> typeColors)
    {
        if (IsMouseCaptured) FinishDrag();
        ApplyTheme(light);
        foreach (Paper paper in papers) assembly.Children.Remove(paper.Model);
        papers.Clear();
        HangingTask[] displayed = tasks.Take(10).Reverse().ToArray();
        double spacing = displayed.Length > 1 ? Math.Min(0.18, 1.28 / (displayed.Length - 1)) : 0;
        for (int index = 0; index < displayed.Length; index++)
        {
            HangingTask task = displayed[index];
            uint? tint = task.taskTypeID is Guid typeId && typeColors.TryGetValue(typeId, out uint color) ? color : null;
            Paper paper = CreatePaper(task, index, spacing, chinese, tint);
            papers.Add(paper);
            assembly.Children.Add(paper.Model);
        }
    }

    public void ResizeReceipt(Guid id, double scale)
    {
        Paper? paper = papers.FirstOrDefault(p => p.Task.id == id);
        if (paper == null) return;
        paper.Scale.ScaleX = paper.Scale.ScaleY = paper.Scale.ScaleZ = Math.Clamp(scale, 0.6, 1.5);
        ClampPaper(paper);
        SavePosition(paper);
    }

    public void PlaceReceiptAt(Guid id, Point point)
    {
        Paper? paper = papers.FirstOrDefault(p => p.Task.id == id);
        if (paper == null) return;
        var ray = PointerRay(point);
        Point3D anchor = assembly.Transform.Transform(new Point3D(0, PivotHeight, 0));
        Vector3D normal = camera.LookDirection; normal.Normalize();
        double divisor = Vector3D.DotProduct(ray.Direction, normal);
        if (Math.Abs(divisor) < 0.000001) return;
        double distance = Vector3D.DotProduct(anchor - ray.Origin, normal) / divisor;
        Matrix3D inverse = assembly.Transform.Value; inverse.Invert();
        Point3D local = inverse.Transform(ray.Origin + ray.Direction * distance);
        paper.Translation.OffsetX = local.X;
        paper.Translation.OffsetY = local.Y;
        paper.Translation.OffsetZ = local.Z;
        ClampPaper(paper);
        SavePosition(paper);
    }

    private void BuildScene()
    {
        // Match the macOS cylinder, torus rim, shallow top, metal shaft and cone.
        AddBase(Frustum(1.18, 1.18, 0.02, 0.30, 96, true, true));
        AddBase(Torus(1.125, 0.052, 0.285, 96, 20));
        topModel = new GeometryModel3D { Geometry = Frustum(1.10, 1.10, 0.2975, 0.3525, 96, true, true) };
        assembly.Children.Add(topModel);
        Material metal = MetalMaterial(Color.FromRgb(133, 133, 133), Color.FromRgb(240, 240, 240), 90);
        AddMetal(Frustum(0.055, 0.055, 0.35, 3.01, 48, true, true), metal);
        AddMetal(Frustum(0.069, 0, 3.01, 3.43, 48, true, false), metal);
        AddMetal(Torus(0.09, 0.028, 0.37, 48, 16), metal);

        Transform3DGroup rotation = new Transform3DGroup();
        rotation.Children.Add(new RotateTransform3D(yawRotation, new Point3D(0, PivotHeight, 0)));
        rotation.Children.Add(new RotateTransform3D(pitchRotation, new Point3D(0, PivotHeight, 0)));
        assembly.Transform = rotation;
        UpdateRotation();
        Model3DGroup scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(48, 48, 48)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(205, 205, 205), new Vector3D(-3.5, -4.62, -4.7)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(85, 85, 85), new Vector3D(4.5, -2.02, -2.5)));
        scene.Children.Add(assembly);
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        camera.Position = new Point3D(3.55, 4.25, 4.35);
        camera.LookDirection = new Vector3D(-3.55, 1.58 - 4.25, -4.35);
        camera.UpDirection = new Vector3D(0, 1, 0);
        camera.NearPlaneDistance = 0.1;
        camera.FarPlaneDistance = 100;
        viewport.Camera = camera;
        ApplyTheme(true);
    }

    private void AddBase(MeshGeometry3D mesh)
    {
        GeometryModel3D model = new GeometryModel3D { Geometry = mesh };
        baseModels.Add(model);
        assembly.Children.Add(model);
    }

    private void AddMetal(MeshGeometry3D mesh, Material material)
    {
        assembly.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private void ApplyTheme(bool light)
    {
        if (lightAppearance == light) return;
        lightAppearance = light;
        Material baseMaterial = MetalMaterial(light ? Color.FromRgb(4, 4, 4) : Color.FromRgb(199, 199, 199),
            light ? Color.FromRgb(105, 105, 105) : Colors.White, 85);
        foreach (GeometryModel3D model in baseModels)
        {
            model.Material = baseMaterial;
            model.BackMaterial = baseMaterial;
        }
        topModel.Material = MetalMaterial(light ? Color.FromRgb(10, 10, 10) : Color.FromRgb(240, 240, 240),
            light ? Color.FromRgb(60, 60, 60) : Colors.White, 110);
        topModel.BackMaterial = topModel.Material;
    }

    private void UpdateRotation()
    {
        yawRotation.Angle = yaw * 180 / Math.PI;
        pitchRotation.Angle = pitch * 180 / Math.PI;
    }

    private void UpdateCamera()
    {
        // WPF specifies horizontal FOV; SceneKit's camera specifies vertical FOV.
        double aspect = Math.Max(1, ActualWidth) / Math.Max(1, ActualHeight);
        camera.FieldOfView = 2 * Math.Atan(Math.Tan(verticalFieldOfView * Math.PI / 360) * aspect) * 180 / Math.PI;
    }

    private Paper CreatePaper(HangingTask task, int index, double spacing, bool chinese, uint? tint)
    {
        double width = 2.82 - index % 2 * 0.10;
        double depth = 1.34 + index % 3 * 0.045;
        BitmapSource texture = ReceiptTexture(task, chinese, tint);
        MaterialGroup material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(Brushes.Black));
        material.Children.Add(new EmissiveMaterial(new ImageBrush(texture)));
        material.Freeze();
        GeometryModel3D model = new GeometryModel3D(PaperMesh(width, depth, index), material) { BackMaterial = material };
        ScaleTransform3D scale = new ScaleTransform3D(task.EffectiveSpikeScale, task.EffectiveSpikeScale, task.EffectiveSpikeScale);
        double[] angles = { -0.23, 0.14, -0.09, 0.21, -0.16 };
        double[] rolls = { 0.025, -0.018, 0.014, -0.025, 0.018 };
        TranslateTransform3D translation;
        if (task.spikePositionVersion == 1)
            translation = new TranslateTransform3D(task.spikeX ?? 0, task.spikeY ?? 1.34 + index * spacing, task.spikeZ ?? 0);
        else
            // Earlier Windows builds stored normalized X/depth and used Z for ordering.
            translation = new TranslateTransform3D((task.spikeX.GetValueOrDefault(0.5) - 0.5) * 1.4,
                1.34 + index * spacing, (task.spikeY.GetValueOrDefault(0.5) - 0.5) * 0.5);
        Transform3DGroup transform = new Transform3DGroup();
        transform.Children.Add(scale);
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), angles[index % 5] * 180 / Math.PI)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), rolls[index % 5] * 180 / Math.PI)));
        transform.Children.Add(translation);
        model.Transform = transform;
        Paper paper = new Paper(task, model, translation, scale, width, depth);
        ClampPaper(paper);
        return paper;
    }

    private static MeshGeometry3D PaperMesh(double width, double depth, int index)
    {
        const int columns = 16, rows = 8;
        MeshGeometry3D mesh = new MeshGeometry3D();
        double phase = index * 0.83;
        for (int row = 0; row <= rows; row++)
        {
            double v = (double)row / rows;
            double z = (v - 0.5) * depth;
            for (int column = 0; column <= columns; column++)
            {
                double u = (double)column / columns;
                double x = (u - 0.5) * width;
                double edgeLift = Math.Pow(Math.Abs(x) / (width * 0.5), 2) * 0.045;
                double wave = Math.Sin(x * 2.45 + phase) * 0.038 + Math.Cos(z * 4.1 - phase) * 0.022 + edgeLift;
                mesh.Positions.Add(new Point3D(x, wave, z));
                mesh.Normals.Add(new Vector3D(0, 1, 0));
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int a = row * (columns + 1) + column, b = a + 1;
                int c = (row + 1) * (columns + 1) + column, d = c + 1;
                AddTriangle(mesh, a, c, b); AddTriangle(mesh, b, c, d);
            }
        mesh.Freeze();
        return mesh;
    }

    private static BitmapSource ReceiptTexture(HangingTask task, bool chinese, uint? tint)
    {
        ReceiptPaperStyle style = ReceiptPaperStyle.For(tint);
        Color ink = style.Ink.Color, muted = style.MutedInk.Color;
        DrawingVisual visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(style.Surface, null, new Rect(0, 0, 720, 340));
            Pen rule = new Pen(new SolidColorBrush(ink), 3);
            drawing.DrawRectangle(null, rule, new Rect(18, 18, 684, 304));
            drawing.DrawLine(rule, new Point(18, 214), new Point(702, 214));
            Pen headerRule = new Pen(new SolidColorBrush(ink), 2);
            drawing.DrawLine(headerRule, new Point(18, 62), new Point(702, 62));
            drawing.DrawLine(headerRule, new Point(548, 18), new Point(548, 62));
            Typeface labelFont = new Typeface((FontFamily)Application.Current.Resources["MonoFont"], FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Typeface displayFont = new Typeface((FontFamily)Application.Current.Resources["DisplayFont"], FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            DrawText(drawing, "HANG LATER", labelFont, 17, ink, new Point(560, 28), 132);
            DrawText(drawing, "✓", labelFont, 29, ink, new Point(36, 119), 32);
            string title = new string(task.title.Take(55).ToArray()).ToUpperInvariant();
            FormattedText titleText = Text(title, displayFont, 38, ink);
            titleText.MaxTextWidth = 600; titleText.MaxTextHeight = 94;
            titleText.Trimming = TextTrimming.CharacterEllipsis;
            drawing.DrawText(titleText, new Point(72, 95));
            DrawText(drawing, (chinese ? "发布  " : "PUBLISHED  ") + task.createdAt.ToLocalTime().ToString("yyyy.MM.dd  HH:mm"), labelFont, 22, muted, new Point(44, 240), 640);
            DrawText(drawing, (chinese ? "完成  " : "COMPLETED  ") + (task.completedAt ?? task.createdAt).ToLocalTime().ToString("yyyy.MM.dd  HH:mm"), labelFont, 22, muted, new Point(44, 279), 640);
        }
        RenderTargetBitmap bitmap = new RenderTargetBitmap(720, 340, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        // Detach the drawing render target before using it as a 3D texture.
        // Otherwise the WPF compositor can sample an unfinished white surface.
        byte[] pixels = new byte[720 * 340 * 4];
        bitmap.CopyPixels(pixels, 720 * 4, 0);
        BitmapSource texture = BitmapSource.Create(720, 340, 96, 96, PixelFormats.Pbgra32, null, pixels, 720 * 4);
        texture.Freeze();
        return texture;
    }

    private static FormattedText Text(string value, Typeface font, double size, Color color) =>
        new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, font, size, new SolidColorBrush(color), 1);

    private static void DrawText(DrawingContext drawing, string value, Typeface font, double size, Color color, Point point, double width)
    {
        FormattedText text = Text(value, font, size, color);
        text.MaxTextWidth = width; text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
        drawing.DrawText(text, point);
    }

    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        Focus();
        Point point = e.GetPosition(viewport);
        if (e.ClickCount == 2)
        {
            yaw = 0.42; pitch = -0.03; verticalFieldOfView = initialVerticalFieldOfView;
            UpdateRotation(); UpdateCamera(); e.Handled = true; return;
        }
        lastPointer = point; dragDistance = 0;
        (Paper Paper, Point3D Hit)? hit = HitPaper(point);
        activePaper = hit?.Paper;
        if (hit.HasValue)
        {
            dragPlanePoint = hit.Value.Hit;
            dragPlaneNormal = camera.LookDirection; dragPlaneNormal.Normalize();
            Matrix3D inverse = assembly.Transform.Value; inverse.Invert();
            Point3D localHit = inverse.Transform(hit.Value.Hit);
            grabOffset = new Vector3D(activePaper!.Translation.OffsetX - localHit.X,
                activePaper.Translation.OffsetY - localHit.Y, activePaper.Translation.OffsetZ - localHit.Z);
        }
        Cursor = Cursors.SizeAll;
        CaptureMouse(); e.Handled = true;
    }

    private void ContinueDrag(object sender, MouseEventArgs e)
    {
        if (!lastPointer.HasValue || e.LeftButton != MouseButtonState.Pressed) return;
        Point point = e.GetPosition(viewport);
        Vector delta = point - lastPointer.Value;
        dragDistance += Math.Abs(delta.X) + Math.Abs(delta.Y);
        lastPointer = point;
        if (activePaper != null)
        {
            (Point3D Origin, Vector3D Direction) ray = PointerRay(point);
            double divisor = Vector3D.DotProduct(ray.Direction, dragPlaneNormal);
            if (Math.Abs(divisor) > 0.000001)
            {
                double distance = Vector3D.DotProduct(dragPlanePoint - ray.Origin, dragPlaneNormal) / divisor;
                Point3D world = ray.Origin + ray.Direction * distance;
                Matrix3D inverse = assembly.Transform.Value; inverse.Invert();
                Point3D local = inverse.Transform(world) + grabOffset;
                activePaper.Translation.OffsetX = local.X;
                activePaper.Translation.OffsetY = local.Y;
                activePaper.Translation.OffsetZ = local.Z;
                ClampPaper(activePaper);
            }
        }
        else
        {
            yaw = (yaw + delta.X * 0.012) % (Math.PI * 2);
            pitch = Math.Clamp(pitch + delta.Y * 0.0045, -0.12, 0.18);
            UpdateRotation();
        }
        e.Handled = true;
    }

    private static void ClampPaper(Paper paper)
    {
        // Retain a margin around the shaft even at the minimum paper scale.
        double limitX = Math.Min(0.95, paper.Width * paper.Scale.ScaleX * 0.5 - 0.16);
        double limitZ = Math.Min(0.38, paper.Depth * paper.Scale.ScaleX * 0.5 - 0.12);
        paper.Translation.OffsetX = Math.Clamp(paper.Translation.OffsetX, -limitX, limitX);
        paper.Translation.OffsetY = Math.Clamp(paper.Translation.OffsetY, MinimumPaperHeight, MaximumPaperHeight);
        paper.Translation.OffsetZ = Math.Clamp(paper.Translation.OffsetZ, -limitZ, limitZ);
        // A sheet has its own yaw and slight roll. Constrain the actual piercing point
        // in sheet coordinates, including when a small sheet is dragged diagonally.
        Matrix3D transform = paper.Model.Transform.Value;
        double determinant = transform.M11 * transform.M33 - transform.M31 * transform.M13;
        if (Math.Abs(determinant) > 0.000001)
        {
            double x = (-paper.Translation.OffsetX * transform.M33 + transform.M31 * paper.Translation.OffsetZ) / determinant;
            double z = (-transform.M11 * paper.Translation.OffsetZ + transform.M13 * paper.Translation.OffsetX) / determinant;
            x = Math.Clamp(x, -paper.Width * 0.5 + 0.12, paper.Width * 0.5 - 0.12);
            z = Math.Clamp(z, -paper.Depth * 0.5 + 0.10, paper.Depth * 0.5 - 0.10);
            paper.Translation.OffsetX = -(x * transform.M11 + z * transform.M31);
            paper.Translation.OffsetZ = -(x * transform.M13 + z * transform.M33);
        }
    }

    private void FinishDrag()
    {
        Paper? paper = activePaper;
        activePaper = null; lastPointer = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Cursor = Cursors.Hand;
        if (paper == null) return;
        if (dragDistance < 4) ReceiptClicked?.Invoke(paper.Task.id);
        else SavePosition(paper);
    }

    private void SavePosition(Paper paper)
    {
        paper.Task.spikeX = paper.Translation.OffsetX;
        paper.Task.spikeY = paper.Translation.OffsetY;
        paper.Task.spikeZ = paper.Translation.OffsetZ;
        paper.Task.spikePositionVersion = 1;
        ReceiptMoved?.Invoke(paper.Task.id, paper.Translation.OffsetX, paper.Translation.OffsetY, paper.Translation.OffsetZ);
    }

    private void OnZoom(object sender, MouseWheelEventArgs e)
    {
        if (UseControlWheelForZoom && (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        (Paper Paper, Point3D Hit)? hit = HitPaper(e.GetPosition(viewport));
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && hit.HasValue)
        {
            Paper paper = hit.Value.Paper;
            double scale = Math.Clamp(paper.Scale.ScaleX + e.Delta / 120.0 * 0.05, 0.6, 1.5);
            paper.Scale.ScaleX = paper.Scale.ScaleY = paper.Scale.ScaleZ = scale;
            paper.Task.spikeScale = scale;
            ClampPaper(paper); SavePosition(paper);
            ReceiptResized?.Invoke(paper.Task.id, scale);
        }
        else
        {
            verticalFieldOfView = Math.Clamp(verticalFieldOfView - e.Delta / 120.0 * 1.8, 29, 44);
            UpdateCamera();
        }
        e.Handled = true;
    }

    private void OnPaperKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Home) return;
        yaw = 0.42; pitch = -0.03; verticalFieldOfView = initialVerticalFieldOfView;
        UpdateRotation(); UpdateCamera(); e.Handled = true;
    }

    private (Point3D Origin, Vector3D Direction) PointerRay(Point point)
    {
        Vector3D forward = camera.LookDirection; forward.Normalize();
        Vector3D right = Vector3D.CrossProduct(forward, camera.UpDirection); right.Normalize();
        Vector3D up = Vector3D.CrossProduct(right, forward); up.Normalize();
        double tangent = Math.Tan(camera.FieldOfView * Math.PI / 360);
        double width = Math.Max(1, viewport.ActualWidth), height = Math.Max(1, viewport.ActualHeight);
        Vector3D direction = forward + right * ((point.X / width * 2 - 1) * tangent)
            + up * ((1 - point.Y / height * 2) * tangent * height / width);
        direction.Normalize();
        return (camera.Position, direction);
    }

    private (Paper Paper, Point3D Hit)? HitPaper(Point point)
    {
        var ray = PointerRay(point);
        double nearest = double.PositiveInfinity;
        (Paper Paper, Point3D Hit)? hit = null;
        foreach (Paper paper in papers)
        {
            MeshGeometry3D mesh = (MeshGeometry3D)paper.Model.Geometry;
            Point3D[] positions = mesh.Positions.Select(p => assembly.Transform.Transform(paper.Model.Transform.Transform(p))).ToArray();
            for (int triangle = 0; triangle < mesh.TriangleIndices.Count; triangle += 3)
            {
                Point3D a = positions[mesh.TriangleIndices[triangle]], b = positions[mesh.TriangleIndices[triangle + 1]], c = positions[mesh.TriangleIndices[triangle + 2]];
                if (RayTriangle(ray.Origin, ray.Direction, a, b, c, out double distance) && distance < nearest)
                {
                    nearest = distance; hit = (paper, ray.Origin + ray.Direction * distance);
                }
            }
        }
        return hit;
    }

    private static bool RayTriangle(Point3D origin, Vector3D direction, Point3D a, Point3D b, Point3D c, out double distance)
    {
        distance = 0;
        Vector3D edge1 = b - a, edge2 = c - a;
        Vector3D cross = Vector3D.CrossProduct(direction, edge2);
        double determinant = Vector3D.DotProduct(edge1, cross);
        if (Math.Abs(determinant) < 0.0000001) return false;
        double inverse = 1 / determinant;
        Vector3D offset = origin - a;
        double u = Vector3D.DotProduct(offset, cross) * inverse;
        if (u < 0 || u > 1) return false;
        Vector3D q = Vector3D.CrossProduct(offset, edge1);
        double v = Vector3D.DotProduct(direction, q) * inverse;
        if (v < 0 || u + v > 1) return false;
        distance = Vector3D.DotProduct(edge2, q) * inverse;
        return distance > 0;
    }

    private static Material MetalMaterial(Color diffuse, Color specular, double power)
    {
        MaterialGroup material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuse)));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(specular), power));
        material.Freeze(); return material;
    }

    private static MeshGeometry3D Frustum(double bottomRadius, double topRadius, double bottomY, double topY, int segments, bool capBottom, bool capTop)
    {
        MeshGeometry3D mesh = new MeshGeometry3D();
        double slope = (bottomRadius - topRadius) / (topY - bottomY);
        for (int i = 0; i <= segments; i++)
        {
            double angle = Math.PI * 2 * i / segments, cosine = Math.Cos(angle), sine = Math.Sin(angle);
            Vector3D normal = new Vector3D(cosine, slope, sine); normal.Normalize();
            mesh.Positions.Add(new Point3D(bottomRadius * cosine, bottomY, bottomRadius * sine));
            mesh.Positions.Add(new Point3D(topRadius * cosine, topY, topRadius * sine));
            mesh.Normals.Add(normal); mesh.Normals.Add(normal);
            mesh.TextureCoordinates.Add(new Point((double)i / segments, 1));
            mesh.TextureCoordinates.Add(new Point((double)i / segments, 0));
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            AddTriangle(mesh, a, a + 1, a + 3); AddTriangle(mesh, a, a + 3, a + 2);
        }
        if (capBottom) Cap(mesh, bottomRadius, bottomY, segments, false);
        if (capTop) Cap(mesh, topRadius, topY, segments, true);
        mesh.Freeze(); return mesh;
    }

    private static void Cap(MeshGeometry3D mesh, double radius, double y, int segments, bool top)
    {
        int center = mesh.Positions.Count;
        Vector3D normal = new Vector3D(0, top ? 1 : -1, 0);
        mesh.Positions.Add(new Point3D(0, y, 0)); mesh.Normals.Add(normal); mesh.TextureCoordinates.Add(new Point(0.5, 0.5));
        int ring = mesh.Positions.Count;
        for (int i = 0; i <= segments; i++)
        {
            double angle = Math.PI * 2 * i / segments, x = radius * Math.Cos(angle), z = radius * Math.Sin(angle);
            mesh.Positions.Add(new Point3D(x, y, z)); mesh.Normals.Add(normal);
            mesh.TextureCoordinates.Add(new Point(0.5 + Math.Cos(angle) * 0.5, 0.5 - Math.Sin(angle) * 0.5));
        }
        for (int i = 0; i < segments; i++) AddTriangle(mesh, center, top ? ring + i + 1 : ring + i, top ? ring + i : ring + i + 1);
    }

    private static MeshGeometry3D Torus(double radius, double tube, double y, int segments, int rings)
    {
        MeshGeometry3D mesh = new MeshGeometry3D();
        for (int i = 0; i <= segments; i++)
        {
            double u = Math.PI * 2 * i / segments;
            for (int j = 0; j <= rings; j++)
            {
                double v = Math.PI * 2 * j / rings;
                mesh.Positions.Add(new Point3D((radius + tube * Math.Cos(v)) * Math.Cos(u), y + tube * Math.Sin(v), (radius + tube * Math.Cos(v)) * Math.Sin(u)));
                mesh.Normals.Add(new Vector3D(Math.Cos(v) * Math.Cos(u), Math.Sin(v), Math.Cos(v) * Math.Sin(u)));
                mesh.TextureCoordinates.Add(new Point((double)i / segments, (double)j / rings));
            }
        }
        for (int i = 0; i < segments; i++)
            for (int j = 0; j < rings; j++)
            {
                int a = i * (rings + 1) + j, b = a + rings + 1;
                AddTriangle(mesh, a, a + 1, b); AddTriangle(mesh, a + 1, b + 1, b);
            }
        mesh.Freeze(); return mesh;
    }

    private static void AddTriangle(MeshGeometry3D mesh, int a, int b, int c)
    {
        mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(c);
    }
}
