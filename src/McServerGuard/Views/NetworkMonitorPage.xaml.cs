using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using McServerGuard.ViewModels;

namespace McServerGuard.Views;

public partial class NetworkMonitorPage : Page
{
    private NetworkMonitorViewModel? _viewModel;
    private DispatcherTimer? _updateTimer;

    public NetworkMonitorPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as NetworkMonitorViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateCylinder();
        }

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer?.Stop();
        _updateTimer?.Tick -= OnUpdateTimerTick;

        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        (_viewModel as IDisposable)?.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(_viewModel.SystemPorts) or 
                            nameof(_viewModel.RegisteredPorts) or 
                            nameof(_viewModel.DynamicPorts))
        {
            UpdateCylinder();
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        UpdateCylinder();
    }

    private void UpdateCylinder()
    {
        if (_viewModel == null)
            return;

        var systemCount = _viewModel.SystemPorts;
        var registeredCount = _viewModel.RegisteredPorts;
        var dynamicCount = _viewModel.DynamicPorts;

        CylinderMesh.Positions = CreateCylinderPositions(systemCount, registeredCount, dynamicCount);
        CylinderMesh.TriangleIndices = CreateCylinderIndices(systemCount, registeredCount, dynamicCount);
        CylinderMesh.Normals = CreateCylinderNormals(systemCount, registeredCount, dynamicCount);
        CylinderMesh.TextureCoordinates = CreateCylinderTextureCoords(systemCount, registeredCount, dynamicCount);
    }

    private Point3DCollection CreateCylinderPositions(int systemCount, int registeredCount, int dynamicCount)
    {
        var positions = new Point3DCollection();
        const int segments = 32;
        const double radius = 1;
        const double height = 3;

        var systemHeight = Math.Min((double)systemCount / 50 * height, height);
        var registeredHeight = Math.Min((double)registeredCount / 500 * height, height);
        var dynamicHeight = Math.Min((double)dynamicCount / 1000 * height, height);

        var maxHeight = Math.Max(Math.Max(systemHeight, registeredHeight), dynamicHeight);
        if (maxHeight < 0.1) maxHeight = 0.1;

        for (int i = 0; i < segments; i++)
        {
            double angle = (i * 2 * Math.PI) / segments;
            double x = radius * Math.Cos(angle);
            double z = radius * Math.Sin(angle);

            positions.Add(new Point3D(x, -maxHeight / 2, z));
            positions.Add(new Point3D(x, maxHeight / 2, z));
        }

        return positions;
    }

    private Int32Collection CreateCylinderIndices(int systemCount, int registeredCount, int dynamicCount)
    {
        var indices = new Int32Collection();
        const int segments = 32;

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            indices.Add(i * 2);
            indices.Add(next * 2);
            indices.Add(i * 2 + 1);

            indices.Add(i * 2 + 1);
            indices.Add(next * 2);
            indices.Add(next * 2 + 1);
        }

        return indices;
    }

    private Vector3DCollection CreateCylinderNormals(int systemCount, int registeredCount, int dynamicCount)
    {
        var normals = new Vector3DCollection();
        const int segments = 32;

        for (int i = 0; i < segments; i++)
        {
            double angle = (i * 2 * Math.PI) / segments;
            double nx = Math.Cos(angle);
            double nz = Math.Sin(angle);

            normals.Add(new Vector3D(nx, 0, nz));
            normals.Add(new Vector3D(nx, 0, nz));
        }

        return normals;
    }

    private PointCollection CreateCylinderTextureCoords(int systemCount, int registeredCount, int dynamicCount)
    {
        var coords = new PointCollection();
        const int segments = 32;

        for (int i = 0; i < segments; i++)
        {
            double u = (double)i / segments;
            coords.Add(new Point(u, 0));
            coords.Add(new Point(u, 1));
        }

        return coords;
    }
}