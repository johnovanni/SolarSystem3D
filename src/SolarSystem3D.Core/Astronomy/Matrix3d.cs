namespace SolarSystem3D.Core.Astronomy;

/// <summary>Row-major 3x3 matrix used for frame rotations.</summary>
public readonly struct Matrix3d
{
    private readonly double _m11, _m12, _m13, _m21, _m22, _m23, _m31, _m32, _m33;

    public Matrix3d(double m11, double m12, double m13,
                    double m21, double m22, double m23,
                    double m31, double m32, double m33)
    {
        _m11 = m11; _m12 = m12; _m13 = m13;
        _m21 = m21; _m22 = m22; _m23 = m23;
        _m31 = m31; _m32 = m32; _m33 = m33;
    }

    public static Matrix3d Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    /// <summary>Builds a matrix whose columns are the given basis vectors.</summary>
    public static Matrix3d FromColumns(Vector3d c1, Vector3d c2, Vector3d c3) =>
        new(c1.X, c2.X, c3.X,
            c1.Y, c2.Y, c3.Y,
            c1.Z, c2.Z, c3.Z);

    /// <summary>Active rotation about the X axis by <paramref name="angle"/> radians.</summary>
    public static Matrix3d RotationX(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new(1, 0, 0, 0, c, -s, 0, s, c);
    }

    /// <summary>Active rotation about the Z axis by <paramref name="angle"/> radians.</summary>
    public static Matrix3d RotationZ(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new(c, -s, 0, s, c, 0, 0, 0, 1);
    }

    public static Vector3d operator *(Matrix3d m, Vector3d v) =>
        new(m._m11 * v.X + m._m12 * v.Y + m._m13 * v.Z,
            m._m21 * v.X + m._m22 * v.Y + m._m23 * v.Z,
            m._m31 * v.X + m._m32 * v.Y + m._m33 * v.Z);

    public static Matrix3d operator *(Matrix3d a, Matrix3d b) =>
        new(a._m11 * b._m11 + a._m12 * b._m21 + a._m13 * b._m31,
            a._m11 * b._m12 + a._m12 * b._m22 + a._m13 * b._m32,
            a._m11 * b._m13 + a._m12 * b._m23 + a._m13 * b._m33,
            a._m21 * b._m11 + a._m22 * b._m21 + a._m23 * b._m31,
            a._m21 * b._m12 + a._m22 * b._m22 + a._m23 * b._m32,
            a._m21 * b._m13 + a._m22 * b._m23 + a._m23 * b._m33,
            a._m31 * b._m11 + a._m32 * b._m21 + a._m33 * b._m31,
            a._m31 * b._m12 + a._m32 * b._m22 + a._m33 * b._m32,
            a._m31 * b._m13 + a._m32 * b._m23 + a._m33 * b._m33);
}
