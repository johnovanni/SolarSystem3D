using System.Windows.Media.Media3D;

namespace SolarSystem3D.App.Rendering;

/// <summary>Builds the shared meshes used for bodies and rings.</summary>
public static class MeshFactory
{
    /// <summary>Unit sphere centred on the origin.</summary>
    public static MeshGeometry3D UnitSphere(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        for (int st = 0; st <= stacks; st++)
        {
            double phi = Math.PI * st / stacks;
            double z = Math.Cos(phi), r = Math.Sin(phi);
            for (int sl = 0; sl <= slices; sl++)
            {
                double theta = 2 * Math.PI * sl / slices;
                var n = new Vector3D(r * Math.Cos(theta), r * Math.Sin(theta), z);
                mesh.Positions.Add(new Point3D(n.X, n.Y, n.Z));
                mesh.Normals.Add(n);
            }
        }
        int row = slices + 1;
        for (int st = 0; st < stacks; st++)
        for (int sl = 0; sl < slices; sl++)
        {
            int a = st * row + sl, b = a + row;
            mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(a + 1);
            mesh.TriangleIndices.Add(a + 1); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1);
        }
        mesh.Freeze();
        return mesh;
    }

    /// <summary>Flat annulus in the XY plane with radii relative to an outer radius of 1.</summary>
    public static MeshGeometry3D Ring(double innerFraction, int segments)
    {
        var mesh = new MeshGeometry3D();
        for (int k = 0; k <= segments; k++)
        {
            double t = 2 * Math.PI * k / segments;
            double c = Math.Cos(t), s = Math.Sin(t);
            mesh.Positions.Add(new Point3D(innerFraction * c, innerFraction * s, 0));
            mesh.Positions.Add(new Point3D(c, s, 0));
            mesh.Normals.Add(new Vector3D(0, 0, 1));
            mesh.Normals.Add(new Vector3D(0, 0, 1));
        }
        for (int k = 0; k < segments; k++)
        {
            int i = 2 * k;
            mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
            mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 3);
        }
        mesh.Freeze();
        return mesh;
    }
}
