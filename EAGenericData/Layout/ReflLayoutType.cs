using System;
using System.Numerics;

namespace EAGenericData.Layout
{
    public static class ReflLayoutType
    {
        public static readonly ReflLayout Invalid;
		public static readonly ReflLayout Bool;
		public static readonly ReflLayout Int8;
		public static readonly ReflLayout UInt8;
		public static readonly ReflLayout Int16;
		public static readonly ReflLayout UInt16;
		public static readonly ReflLayout Int32;
		public static readonly ReflLayout UInt32;
		public static readonly ReflLayout Int64;
		public static readonly ReflLayout UInt64;
		public static readonly ReflLayout Float;
		public static readonly ReflLayout Vector2;
		public static readonly ReflLayout Vector3;
		public static readonly ReflLayout Vector4;
		public static readonly ReflLayout Quaternion;
		public static readonly ReflLayout Matrix44;
		public static readonly ReflLayout Guid;
		public static readonly ReflLayout String;
		public static readonly ReflLayout DataRef;
		public static readonly ReflLayout Double;
		public static readonly ReflLayout QuatPos;
		public static readonly ReflLayout Int8Vec16;
		public static readonly ReflLayout UInt8Vec16;
		public static readonly ReflLayout Int16Vec8;
		public static readonly ReflLayout UInt16Vec8;
		public static readonly ReflLayout Int32Vec2;
		public static readonly ReflLayout Int32Vec3;
		public static readonly ReflLayout Int32Vec4;
		public static readonly ReflLayout UInt32Vec2;
		public static readonly ReflLayout UInt32Vec3;
		public static readonly ReflLayout UInt32Vec4;
		public static readonly ReflLayout BoolVec2;
		public static readonly ReflLayout BoolVec3;
		public static readonly ReflLayout BoolVec4;
		public static readonly ReflLayout Matrix33;
		public static readonly ReflLayout Key;
        
		static ReflLayoutType()
		{
            var noFields = Array.Empty<ReflLayoutField>();
            
            Invalid = new ReflLayout("Invalid", 0, 0, ReflLayoutHash.Invalid, noFields);
            Bool    = new ReflLayout("Bool",    1, 1, ReflLayoutHash.Bool,    noFields);
            Int8    = new ReflLayout("Int8",    1, 1, ReflLayoutHash.Int8,    noFields);
            UInt8   = new ReflLayout("UInt8",   1, 1, ReflLayoutHash.UInt8,   noFields);
            Int16   = new ReflLayout("Int16",   2, 2, ReflLayoutHash.Int16,   noFields);
            UInt16  = new ReflLayout("UInt16",  2, 2, ReflLayoutHash.UInt16,  noFields);
            Int32   = new ReflLayout("Int32",   4, 4, ReflLayoutHash.Int32,   noFields);
            UInt32  = new ReflLayout("UInt32",  4, 4, ReflLayoutHash.UInt32,  noFields);
            Int64   = new ReflLayout("Int64",   8, 8, ReflLayoutHash.Int64,   noFields);
            UInt64  = new ReflLayout("UInt64",  8, 8, ReflLayoutHash.UInt64,  noFields);
            Float   = new ReflLayout("Float",   4, 4, ReflLayoutHash.Float,   noFields);
            Double  = new ReflLayout("Double",  8, 8, ReflLayoutHash.Double,  noFields);
            DataRef = new ReflLayout("DataRef", 8, 8, ReflLayoutHash.DataRef, noFields);
            
            String = new ReflLayout("String", 16, 8, ReflLayoutHash.String, new[]
            {
                new ReflLayoutField(0, "chars", Int8, ReflLayoutFlags.Array)
            });
            
            Guid = new ReflLayout("Guid", 16, 4, ReflLayoutHash.Guid, new[]
            {
                new ReflLayoutField(0, "Data1", UInt32),
                new ReflLayoutField(1, "Data2", UInt16),
                new ReflLayoutField(2, "Data3", UInt16),
                new ReflLayoutField(3, "Data4", UInt8, ReflLayoutFlags.None, 8)
            });
            
            Vector2 = new ReflLayout("Vector2", 16, 16, ReflLayoutHash.Vector2, new[]
            {
                new ReflLayoutField(0, "x", Float),
                new ReflLayoutField(1, "y", Float)
            });

            Vector3 = new ReflLayout("Vector3", 16, 16, ReflLayoutHash.Vector3, new[]
            {
                new ReflLayoutField(0, "x", Float),
                new ReflLayoutField(1, "y", Float),
                new ReflLayoutField(2, "z", Float)
            });

            Vector4 = new ReflLayout("Vector4", 16, 16, ReflLayoutHash.Vector4, new[]
            {
                new ReflLayoutField(0, "x", Float),
                new ReflLayoutField(1, "y", Float),
                new ReflLayoutField(2, "z", Float),
                new ReflLayoutField(3, "w", Float)
            });

            Quaternion = new ReflLayout("Quaternion", 16, 16, ReflLayoutHash.Quaternion, new[]
            {
                new ReflLayoutField(0, "x", Float),
                new ReflLayoutField(1, "y", Float),
                new ReflLayoutField(2, "z", Float),
                new ReflLayoutField(3, "w", Float)
            });
            
            Matrix44 = new ReflLayout("Matrix44", 64, 16, ReflLayoutHash.Matrix44, new[]
            {
                new ReflLayoutField(0, "x", Vector4),
                new ReflLayoutField(1, "y", Vector4),
                new ReflLayoutField(2, "z", Vector4),
                new ReflLayoutField(3, "w", Vector4)
            });

            Matrix33 = new ReflLayout("Matrix33", 48, 16, ReflLayoutHash.Matrix33, new[]
            {
                new ReflLayoutField(0, "x", Vector3),
                new ReflLayoutField(1, "y", Vector3),
                new ReflLayoutField(2, "z", Vector4)
            });
            
            QuatPos = new ReflLayout("QuatPos", 32, 16, ReflLayoutHash.QuatPos, new[]
            {
                new ReflLayoutField(0, "Rotation", Quaternion),
                new ReflLayoutField(1, "Position", Vector3)
            });
            
            Int8Vec16 = new ReflLayout("Int8Vec16", 16, 16, ReflLayoutHash.Int8Vec16, new[]
            {
                new ReflLayoutField(0, "chars", Int8, ReflLayoutFlags.None, 16)
            });

            UInt8Vec16 = new ReflLayout("UInt8Vec16", 16, 16, ReflLayoutHash.UInt8Vec16, new[]
            {
                new ReflLayoutField(0, "chars", UInt8, ReflLayoutFlags.None, 16)
            });

            Int16Vec8 = new ReflLayout("Int16Vec8", 16, 16, ReflLayoutHash.Int16Vec8, new[]
            {
                new ReflLayoutField(0, "shorts", Int16, ReflLayoutFlags.None, 8)
            });

            UInt16Vec8 = new ReflLayout("UInt16Vec8", 16, 16, ReflLayoutHash.UInt16Vec8, new[]
            {
                new ReflLayoutField(0, "shorts", UInt16, ReflLayoutFlags.None, 8)
            });
            
            Int32Vec2 = new ReflLayout("Int32Vec2", 16, 16, ReflLayoutHash.Int32Vec2, new[]
            {
                new ReflLayoutField(0, "x", Int32),
                new ReflLayoutField(1, "y", Int32)
            });

            Int32Vec3 = new ReflLayout("Int32Vec3", 16, 16, ReflLayoutHash.Int32Vec3, new[]
            {
                new ReflLayoutField(0, "x", Int32),
                new ReflLayoutField(1, "y", Int32),
                new ReflLayoutField(2, "z", Int32)
            });

            Int32Vec4 = new ReflLayout("Int32Vec4", 16, 16, ReflLayoutHash.Int32Vec4, new[]
            {
                new ReflLayoutField(0, "x", Int32),
                new ReflLayoutField(1, "y", Int32),
                new ReflLayoutField(2, "z", Int32),
                new ReflLayoutField(3, "w", Int32)
            });
            
            UInt32Vec2 = new ReflLayout("UInt32Vec2", 16, 16, ReflLayoutHash.UInt32Vec2, new[]
            {
                new ReflLayoutField(0, "x", UInt32),
                new ReflLayoutField(1, "y", UInt32)
            });

            UInt32Vec3 = new ReflLayout("UInt32Vec3", 16, 16, ReflLayoutHash.UInt32Vec3, new[]
            {
                new ReflLayoutField(0, "x", UInt32),
                new ReflLayoutField(1, "y", UInt32),
                new ReflLayoutField(2, "z", UInt32)
            });

            UInt32Vec4 = new ReflLayout("UInt32Vec4", 16, 16, ReflLayoutHash.UInt32Vec4, new[]
            {
                new ReflLayoutField(0, "x", UInt32),
                new ReflLayoutField(1, "y", UInt32),
                new ReflLayoutField(2, "z", UInt32),
                new ReflLayoutField(3, "w", UInt32)
            });
            
            BoolVec2 = new ReflLayout("BoolVec2", 16, 16, ReflLayoutHash.BoolVec2, new[]
            {
                new ReflLayoutField(0, "x", Bool),
                new ReflLayoutField(1, "y", Bool)
            });

            BoolVec3 = new ReflLayout("BoolVec3", 16, 16, ReflLayoutHash.BoolVec3, new[]
            {
                new ReflLayoutField(0, "x", Bool),
                new ReflLayoutField(1, "y", Bool),
                new ReflLayoutField(2, "z", Bool)
            });

            BoolVec4 = new ReflLayout("BoolVec4", 16, 16, ReflLayoutHash.BoolVec4, new[]
            {
                new ReflLayoutField(0, "x", Bool),
                new ReflLayoutField(1, "y", Bool),
                new ReflLayoutField(2, "z", Bool),
                new ReflLayoutField(3, "w", Bool)
            });
            
            Key = new ReflLayout("Key", 8, 8, ReflLayoutHash.Key, new[]
            {
                new ReflLayoutField(0, "Data1", Int64)
            });
		}
        
        public static ReflLayout FromHash(ReflLayoutHash hash)
        {
            switch (hash)
            {
                case ReflLayoutHash.Invalid: return Invalid;
                case ReflLayoutHash.Bool: return Bool;
                case ReflLayoutHash.Int8: return Int8;
                case ReflLayoutHash.UInt8: return UInt8;
                case ReflLayoutHash.Int16: return Int16;
                case ReflLayoutHash.UInt16: return UInt16;
                case ReflLayoutHash.Int32: return Int32;
                case ReflLayoutHash.UInt32: return UInt32;
                case ReflLayoutHash.Int64: return Int64;
                case ReflLayoutHash.UInt64: return UInt64;
                case ReflLayoutHash.Float: return Float;
                case ReflLayoutHash.Double: return Double;
                case ReflLayoutHash.Vector2: return Vector2;
                case ReflLayoutHash.Vector3: return Vector3;
                case ReflLayoutHash.Vector4: return Vector4;
                case ReflLayoutHash.Quaternion: return Quaternion;
                case ReflLayoutHash.Matrix44: return Matrix44;
                case ReflLayoutHash.Guid: return Guid;
                case ReflLayoutHash.String: return String;
                case ReflLayoutHash.DataRef: return DataRef;
                case ReflLayoutHash.QuatPos: return QuatPos;
                case ReflLayoutHash.Int8Vec16: return Int8Vec16;
                case ReflLayoutHash.UInt8Vec16: return UInt8Vec16;
                case ReflLayoutHash.Int16Vec8: return Int16Vec8;
                case ReflLayoutHash.UInt16Vec8: return UInt16Vec8;
                case ReflLayoutHash.Int32Vec2: return Int32Vec2;
                case ReflLayoutHash.Int32Vec3: return Int32Vec3;
                case ReflLayoutHash.Int32Vec4: return Int32Vec4;
                case ReflLayoutHash.UInt32Vec2: return UInt32Vec2;
                case ReflLayoutHash.UInt32Vec3: return UInt32Vec3;
                case ReflLayoutHash.UInt32Vec4: return UInt32Vec4;
                case ReflLayoutHash.BoolVec2: return BoolVec2;
                case ReflLayoutHash.BoolVec3: return BoolVec3;
                case ReflLayoutHash.BoolVec4: return BoolVec4;
                case ReflLayoutHash.Matrix33: return Matrix33;
                case ReflLayoutHash.Key: return Key;
                default: return null;
            }
        }

        public static Type GetConcreteType(ReflLayoutHash hash)
        {
            if (hash >= ReflLayoutHash.SimpleTypeCount)
            {
                return typeof(ReflLayoutData);
            }
            
            switch (hash)
            {
                case ReflLayoutHash.Bool: return typeof(bool);
                case ReflLayoutHash.Int8: return typeof(sbyte);
                case ReflLayoutHash.UInt8: return typeof(byte);
                case ReflLayoutHash.Int16: return typeof(short);
                case ReflLayoutHash.UInt16: return typeof(ushort);
                case ReflLayoutHash.Int32: return typeof(int);
                case ReflLayoutHash.UInt32: return typeof(uint);
                case ReflLayoutHash.Int64: return typeof(long);
                case ReflLayoutHash.UInt64: return typeof(ulong);
                case ReflLayoutHash.Float: return typeof(float);
                case ReflLayoutHash.Double: return typeof(double);
                case ReflLayoutHash.Vector2: return typeof(Vector2);
                case ReflLayoutHash.Vector3: return typeof(Vector3);
                case ReflLayoutHash.Vector4: return typeof(Vector4);
                case ReflLayoutHash.Quaternion: return typeof(Quaternion);
                case ReflLayoutHash.Matrix44: return typeof(Matrix4x4);
                case ReflLayoutHash.Guid: return typeof(Guid);
                case ReflLayoutHash.String: return typeof(string);
                case ReflLayoutHash.DataRef: return typeof(ReflLayoutData);
                default: throw new InvalidOperationException($"Unhandled type: {hash}");
            }
        }
    }
}