using System;

namespace InkCanvasForClassX.Libraries
{
    /// <summary>
    /// 提供了對平面向量的坐標運算
    /// </summary>
    public class Vector {

        private double _x = 0;
        private double _y = 0;

        public Vector(double x = 0, double y = 0) {
            _x = x;
            _y = y;
        }

        public double X {
            get => _x;
            set => _x = value;
        }

        public double Y {
            get => _y;
            set => _y = value;
        }

        /// <summary>
        /// 將該向量改變為其相反向量
        /// </summary>
        public Vector Negate() {
            _x = -_x;
            _y = -_y;
            return this;
        }

        /// <summary>
        /// 提供一個<c>Vector</c>，返回該<c>Vector</c>的相反向量
        /// </summary>
        public static Vector NegateVector(Vector vec) {
            return new Vector(-vec.X, -vec.Y);
        }

        /// <summary>
        /// Csharp中的<c>Math.Hypot</c>實現
        /// </summary>
        private static double Hypot(params double[] values)
        {
            double sum = 0;
            foreach (var value in values) {
                sum += Math.Pow(value, 2);
            }
            return Math.Sqrt(sum);
        }

        /// <summary>
        /// 獲取該向量的長度
        /// </summary>
        public double Length => Vector.Hypot(_x,_y);

        /// <summary>
        /// 獲取該向量的未開平方的長度
        /// </summary>
        public double LengthSquared => _x * _x + _y * _y;

        /// <summary>
        /// 將該向量和另一個向量相加
        /// </summary>
        public Vector Add(Vector vec)
        {
            _x = _x + vec.X;
            _y = _y + vec.Y;
            return this;
        }

        /// <summary>
        /// 提供兩個<c>Vector</c>，返回相加後的<c>Vector</c>
        /// </summary>
        public static Vector AddVectors(Vector vec1, Vector vec2)
        {
            return new Vector(vec1.X + vec2.X, vec1.Y + vec2.Y);
        }

        /// <summary>
        /// 將該向量減去另一個向量
        /// </summary>
        public Vector Subtract(Vector vec)
        {
            _x = _x - vec.X;
            _y = _y - vec.Y;
            return this;
        }

        /// <summary>
        /// 提供兩個<c>Vector</c>，返回<c><paramref name="vec1"/>-<paramref name="vec2"/></c>後的<c>Vector</c>
        /// </summary>
        public static Vector SubtractVectors(Vector vec1, Vector vec2)
        {
            return new Vector(vec1.X - vec2.X, vec1.Y - vec2.Y);
        }

        /// <summary>
        /// 將該向量除以一個數值
        /// </summary>
        public Vector DivideBy(double n)
        {
            _x = _x / n;
            _y = _y / n;
            return this;
        }

        /// <summary>
        /// 提供兩個<c>Vector</c>，返回<c><paramref name="vec"/>/<paramref name="n"/></c>後的<c>Vector</c>
        /// </summary>
        public static Vector DivideByVector(Vector vec, double n)
        {
            return new Vector(vec.X / n, vec.Y / n);
        }

        /// <summary>
        /// 將該向量乘以一個數值
        /// </summary>
        public Vector Multiply(double n)
        {
            _x = _x * n;
            _y = _y * n;
            return this;
        }

        /// <summary>
        /// 提供兩個<c>Vector</c>，返回<c><paramref name="vec"/>*<paramref name="n"/></c>後的<c>Vector</c>
        /// </summary>
        public static Vector MultiplyVector(Vector vec, double n)
        {
            return new Vector(vec.X * n, vec.Y * n);
        }

        /// <summary>
        /// 將該向量垂直旋轉
        /// </summary>
        public Vector PerpendicularRotation() {
            var _t = _x;
            _x = _y;
            _y = - _t;
            return this;
        }

        /// <summary>
        /// 提供<c>Vector</c>，返回垂直旋轉後的<c>Vector</c>
        /// </summary>
        public static Vector PerpendicularRotationVector(Vector vec)
        {
            return new Vector(vec.Y, - vec.X);
        }

        /// <summary>
        /// 提供兩個<c>Vector</c>，返回兩個<c>Vector</c>點乘後的数值
        /// </summary>
        public static double DotVectors(Vector vec1, Vector vec2)
        {
            return vec1.X * vec2.X + vec1.Y * vec2.Y;
        }

        /// <summary>
        /// 判斷該向量和另外一個向量是否相等
        /// </summary>
        public bool IsEqual(Vector vec)
        {
            return Math.Abs(_x - vec.X) < 0.01 && Math.Abs(_y - vec.Y) < 0.01;
        }

        /// <summary>
        /// 獲取該向量和另一個向量的平方距離
        /// </summary>
        public double DistLengthSquared(Vector vec) {
            var subVec = Vector.SubtractVectors(this, vec);
            return subVec.LengthSquared;
        }

        /// <summary>
        /// 獲取一個向量和另一個向量的平方距離
        /// </summary>
        public static double DistLengthSquaredVectors(Vector vec1, Vector vec2)
        {
            var subVec = Vector.SubtractVectors(vec1, vec2);
            return subVec.LengthSquared;
        }

        /// <summary>
        /// 獲取該向量和另一個向量的距離
        /// </summary>
        public double DistLength(Vector vec) {
            return Vector.Hypot(this._y - vec.Y, this._x - vec.X);
        }

        /// <summary>
        /// 獲取一個向量和另一個向量的距離
        /// </summary>
        public static double DistLengthVectors(Vector vec1, Vector vec2)
        {
            return Vector.Hypot(vec1.Y - vec2.Y, vec1.X - vec2.X);
        }

        /// <summary>
        /// 將該向量修改為其中間向量
        /// </summary>
        public Vector Med(Vector vec) {
            var addVec = new Vector(_x, _y).Add(vec);
            addVec.Multiply(0.5);
            _x = addVec.X;
            _y = addVec.Y;
            return this;
        }

        /// <summary>
        /// 獲取一個向量和另一個向量的中間向量
        /// </summary>
        public static Vector MedVectors(Vector vec1, Vector vec2) {
            var addVec = Vector.AddVectors(vec1, vec2);
            return addVec.Multiply(0.5);
        }

        /// <summary>
        /// 將該向量圍繞另一個向量旋轉r弧度
        /// </summary>
        public Vector Rotate(Vector vec, double r) {
            var sin = Math.Sin(r);
            var cos = Math.Cos(r);

            var px = _x - vec.X;
            var py = _y - vec.Y;

            var nx = px * cos - py * sin;
            var ny = px * sin + py * cos;

            _x = nx + vec.X;
            _y = ny + vec.Y;
            return this;
        }

        /// <summary>
        /// 將一個向量圍繞另一個向量旋轉r弧度
        /// </summary>
        public static Vector RotateVectors(Vector vec1, Vector vec2, double r)
        {
            var sin = Math.Sin(r);
            var cos = Math.Cos(r);

            var px = vec1.X - vec2.X;
            var py = vec1.Y - vec2.Y;

            var nx = px * cos - py * sin;
            var ny = px * sin + py * cos;

            return new Vector(nx + vec2.X, ny + vec2.Y);
        }

        /// <summary>
        /// 將該向量與另一個向量插值
        /// </summary>
        public Vector Interpolate(Vector vec, double t) {
            Add(SubtractVectors(vec, this).Multiply(t));
            return this;
        }

        /// <summary>
        /// 將一個向量與另一個向量插值
        /// </summary>
        public static Vector InterpolateVectors(Vector vec1, Vector vec2, double t) {
            return AddVectors(vec1, SubtractVectors(vec2, vec1).Multiply(t));
        }

        /// <summary>
        /// 將該向量投影在向量<paramref name="vec"/>的方向上，並附加距離<paramref name="c"/>
        /// </summary>
        public Vector Project(Vector vec, double c) {
            Add(vec.Multiply(c));
            return this;
        }

        /// <summary>
        /// 將<paramref name="vec1"/>投影在向量<paramref name="vec2"/>的方向上，並附加距離<paramref name="c"/>
        /// </summary>
        public static Vector ProjectVectors(Vector vec1, Vector vec2, double c) {
            return AddVectors(vec1, MultiplyVector(vec2,c));
        }

        /// <summary>
        /// 取得單位向量
        /// </summary>
        public Vector Unit()
        {
            return DivideByVector(this, Length);
        }

        /// <summary>
        /// 取得單位向量
        /// </summary>
        public static Vector UnitVector(Vector vec)
        {
            return DivideByVector(vec, vec.Length);
        }
    }
}
