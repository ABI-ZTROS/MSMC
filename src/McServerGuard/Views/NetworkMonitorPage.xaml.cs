using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using McServerGuard.ViewModels;

namespace McServerGuard.Views;

public partial class NetworkMonitorPage : UserControl
{
    private NetworkMonitorViewModel? _viewModel;

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
            UpdateCylinders();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        (_viewModel as IDisposable)?.Dispose();
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(_viewModel.SystemPorts) or
                            nameof(_viewModel.RegisteredPorts) or
                            nameof(_viewModel.DynamicPorts))
        {
            UpdateCylinders();
        }
    }

    private void UpdateCylinders()
    {
        if (_viewModel == null)
            return;

        int systemCount = _viewModel.SystemPorts;
        int registeredCount = _viewModel.RegisteredPorts;
        int dynamicCount = _viewModel.DynamicPorts;

        const double maxTotalHeight = 2.5;
        const double radius = 0.7;
        const int segments = 36;

        int total = systemCount + registeredCount + dynamicCount;
        if (total == 0)
        {
            systemCount = 1;
            registeredCount = 1;
            dynamicCount = 1;
            total = 3;
        }

        double systemHeight = (double)systemCount / total * maxTotalHeight;
        double registeredHeight = (double)registeredCount / total * maxTotalHeight;
        double dynamicHeight = (double)dynamicCount / total * maxTotalHeight;

        double bottomY = -maxTotalHeight / 2;

        double systemBottom = bottomY;
        double systemTop = systemBottom + systemHeight;

        double registeredBottom = systemTop;
        double registeredTop = registeredBottom + registeredHeight;

        double dynamicBottom = registeredTop;
        double dynamicTop = dynamicBottom + dynamicHeight;

        ApplyCylinderMesh(SystemCylinderMesh, radius, systemBottom, systemTop, segments);
        ApplyCylinderMesh(RegisteredCylinderMesh, radius, registeredBottom, registeredTop, segments);
        ApplyCylinderMesh(DynamicCylinderMesh, radius, dynamicBottom, dynamicTop, segments);
    }

    private static void ApplyCylinderMesh(MeshGeometry3D mesh, double radius, double bottomY, double topY, int segments)
    {
        var positions = new Point3DCollection(segments * 2 + 2);
        var normals = new Vector3DCollection(segments * 2 + 2);
        var textureCoords = new PointCollection(segments * 2 + 2);
        var indices = new Int32Collection(segments * 6);

        for (int i = 0; i <= segments; i++)
        {
            double angle = (i * 2.0 * Math.PI) / segments;
            double x = radius * Math.Cos(angle);
            double z = radius * Math.Sin(angle);

            positions.Add(new Point3D(x, bottomY, z));
            positions.Add(new Point3D(x, topY, z));

            double nx = Math.Cos(angle);
            double nz = Math.Sin(angle);
            normals.Add(new Vector3D(nx, 0, nz));
            normals.Add(new Vector3D(nx, 0, nz));

            double u = (double)i / segments;
            textureCoords.Add(new Point(u, 0));
            textureCoords.Add(new Point(u, 1));
        }

        for (int i = 0; i < segments; i++)
        {
            int bottom0 = i * 2;
            int top0 = i * 2 + 1;
            int bottom1 = (i + 1) * 2;
            int top1 = (i + 1) * 2 + 1;

            indices.Add(bottom0);
            indices.Add(bottom1);
            indices.Add(top0);

            indices.Add(top0);
            indices.Add(bottom1);
            indices.Add(top1);
        }

        mesh.Positions = positions;
        mesh.Normals = normals;
        mesh.TextureCoordinates = textureCoords;
        mesh.TriangleIndices = indices;
    }
}
