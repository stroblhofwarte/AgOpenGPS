using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS.Classes
{
    public class Polyline
    {
        public List<vec2> Points = new List<vec2>(128);

        public bool ResetPoints, ResetIndexer, Loop;
        public int BufferPoints = int.MinValue, BufferIndex = int.MinValue, BufferPointsCnt = 0, BufferIndexCnt = 0;
        public int winding = 1;
        public double area;
        public void DrawPolyLine(bool Triangles)
        {
            if (Points.Count > 0)
            {
                if (Triangles && BufferIndex == int.MinValue || ResetIndexer || ResetPoints)
                {
                    List<int> Indexer = Points.TriangulatePolygon();

                    if (BufferIndex == int.MinValue) GL.GenBuffers(1, out BufferIndex);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(Indexer.Count * 4), Indexer.ToArray(), BufferUsageHint.StaticDraw);

                    BufferIndexCnt = Indexer.Count;
                    ResetIndexer = false;
                }

                if (BufferPoints == int.MinValue || ResetPoints)
                {
                    if (BufferPoints == int.MinValue) GL.GenBuffers(1, out BufferPoints);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Points.Count * 16), Points.ToArray(), BufferUsageHint.StaticDraw);
                    BufferPointsCnt = Points.Count;
                    ResetPoints = false;
                }

                GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
                GL.VertexPointer(2, VertexPointerType.Double, 0, IntPtr.Zero);
                GL.EnableClientState(ArrayCap.VertexArray);

                if (Triangles)
                {
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                    GL.DrawElements(PrimitiveType.Triangles, BufferIndexCnt, DrawElementsType.UnsignedInt, IntPtr.Zero);
                }
                else if (Loop)
                    GL.DrawArrays(PrimitiveType.LineLoop, 0, BufferPointsCnt);
                else
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, BufferPointsCnt);
            }
        }

        public void RemoveHandle()
        {
            if (BufferPoints != int.MinValue)
            {
                try
                {
                    if (GL.IsBuffer(BufferPoints))
                        GL.DeleteBuffer(BufferPoints);
                    Console.WriteLine(BufferPoints);
                    BufferPoints = int.MinValue;
                }
                catch
                {

                    Console.WriteLine(BufferPoints + "Failed");
                }
            }
            else
                Console.WriteLine("-1");
            if (BufferIndex != int.MinValue)
            {
                try
                {
                    if (GL.IsBuffer(BufferIndex))
                        GL.DeleteBuffer(BufferIndex);
                    Console.WriteLine(BufferIndex);
                    BufferIndex = int.MinValue;
                }
                catch
                {
                    Console.WriteLine(BufferIndex + "Failed");
                }
            }
        }
    }

    public static class StaticClass
    {
        public class VertexPoint
        {
            public vec2 Coords;
            public VertexPoint Next;
            //public VertexPoint Prev;
            public VertexPoint Crossing;
            public double Time = -1;

            public VertexPoint(vec2 coords)
            {
                Coords = coords;
            }
        }

        public static List<VertexPoint> PolyLineStructure(List<vec2> polyLine)
        {
            List<VertexPoint> PolyLine = new List<VertexPoint>();

            for (int i = 0; i < polyLine.Count; i++)
            {
                PolyLine.Add(new VertexPoint(polyLine[i]));
            }

            for (int i = 0; i < PolyLine.Count; i++)
            {
                int Next = (i + 1).Clamp(PolyLine.Count);
                //int Prev = (i - 1).Clamp(PolyLine.Count);

                PolyLine[i].Next = PolyLine[Next];
                //PolyLine[i].Prev = PolyLine[Prev];
            }

            return PolyLine;
        }

        public static int Clamp(this int Idx, int Size)
        {
            return (Size + Idx) % Size;
        }

        public static bool GetLineIntersection(vec2 PointAA, vec2 PointAB, vec2 PointBA, vec2 PointBB, out vec2 Crossing, out double TimeA, bool Limit = false)
        {
            TimeA = -1;
            Crossing = new vec2();
            double denominator = (PointAB.northing - PointAA.northing) * (PointBB.easting - PointBA.easting) - (PointBB.northing - PointBA.northing) * (PointAB.easting - PointAA.easting);

            if (denominator != 0.0)
            {
                TimeA = ((PointBB.northing - PointBA.northing) * (PointAA.easting - PointBA.easting) - (PointAA.northing - PointBA.northing) * (PointBB.easting - PointBA.easting)) / denominator;

                if (Limit || (TimeA > 0.0 && TimeA < 1.0))
                {
                    double TimeB = ((PointAB.northing - PointAA.northing) * (PointAA.easting - PointBA.easting) - (PointAA.northing - PointBA.northing) * (PointAB.easting - PointAA.easting)) / denominator;
                    if (Limit || (TimeB > 0.0 && TimeB < 1.0))
                    {
                        Crossing = PointAA + (PointAB - PointAA) * TimeA;
                        return true;
                    }
                    else return false;
                }
                else return false;
            }
            else return false;
        }

        public static VertexPoint InsertCrossing(vec2 intersectionPoint, VertexPoint currentVertex)
        {
            VertexPoint IntersectionCrossing = new VertexPoint(intersectionPoint)
            {
                Next = currentVertex.Next,
                //Prev = currentVertex
            };
            //currentVertex.Next.Prev = IntersectionCrossing;
            currentVertex.Next = IntersectionCrossing;
            return IntersectionCrossing;
        }

        public static bool PointInPolygon(this List<vec2> Polygon, vec2 pointAA)
        {
            vec2 PointAB = new vec2(0.0, 200000.0);

            int NumCrossings = 0;

            for (int i = 0; i < Polygon.Count; i++)
            {
                vec2 PointBB = Polygon[(i + 1).Clamp(Polygon.Count)];

                if (GetLineIntersection(pointAA, PointAB, Polygon[i], PointBB, out _, out _))
                    NumCrossings += 1;
            }
            return NumCrossings % 2 == 1;
        }

        public static Polyline OffsetAndClipPolylineOne(this Polyline Poly, double width, bool loop, int start = -1, int end = -1, bool add = true)
        {
            List<vec2> OffsetPoints = Poly.Points.OffsetPolyline(width, loop, start, end, add);

            List<Polyline> Output = OffsetPoints.ClipPolyLine(null, loop);

            if (Output.Count > 0)
                return Output[0];
            else
                return new Polyline();
        }

        public static List<Polyline> OffsetAndClipPolylineMulti(this Polyline Poly, double width, bool loop, int start = -1, int end = -1, bool add = true)
        {
            List<vec2> OffsetPoints = Poly.Points.OffsetPolyline(width, loop, start, end, add);

            List<Polyline> Output = OffsetPoints.ClipPolyLine(null, loop);

            return Output;
        }

        public static List<vec2> OffsetPolyline(this List<vec2> Points, double Distance, bool Loop, int Start = -1, int End = -1, bool Add = true)
        {
            List<vec2> OffsetPoints = new List<vec2>();
            int A, C;
            double Dx, Dy, Dx1, Dy1, Dx2, Dy2, Length, Northing, Easting;
            for (int i = 0; i < Points.Count; i++)
            {
                if (Start == -1 || (Start > End && (i >= Start || i <= End)) || (Start < End && i >= Start && i <= End))
                {
                    A = (i == 0) ? Points.Count - 1 : i - 1;
                    C = (i + 1 == Points.Count) ? 0 : i + 1;

                    if (!Loop && i == 0)
                    {
                        Dx = Points[C].northing - Points[i].northing;
                        Dy = Points[C].easting - Points[i].easting;
                        Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                        Northing = Distance * (-Dy / Length);
                        Easting = Distance * (Dx / Length);
                        OffsetPoints.Add(new vec2(Points[i].easting + Easting, Points[i].northing + Northing));
                    }
                    else if (!Loop && i == Points.Count - 1)
                    {
                        Dx = Points[i].northing - Points[A].northing;
                        Dy = Points[i].easting - Points[A].easting;
                        Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                        Northing = Distance * (-Dy / Length);
                        Easting = Distance * (Dx / Length);
                        OffsetPoints.Add(new vec2(Points[i].easting + Easting, Points[i].northing + Northing));
                    }
                    else
                    {
                        Dx1 = Points[i].northing - Points[A].northing;
                        Dy1 = Points[i].easting - Points[A].easting;
                        Dx2 = Points[i].northing - Points[C].northing;
                        Dy2 = Points[i].easting - Points[C].easting;
                        double angle = Math.Atan2(Dy1, Dx1) - Math.Atan2(Dy2, Dx2);

                        if (angle < 0) angle += glm.twoPI;
                        if (angle > glm.twoPI) angle -= glm.twoPI;

                        double tan = Math.Abs(Math.Tan(angle / 2.0));

                        double segment = Distance / tan;
                        var p1Cross = Points[i].GetProportionPoint(segment, GetLength(Dx1, Dy1), Dx1, Dy1);
                        var p2Cross = Points[i].GetProportionPoint(segment, GetLength(Dx2, Dy2), Dx2, Dy2);

                        Dx = Points[i].northing * 2 - p1Cross.northing - p2Cross.northing;
                        Dy = Points[i].easting * 2 - p1Cross.easting - p2Cross.easting;

                        if (Dx1 == 0 && Dy1 == 0 || Dx2 == 0 && Dy2 == 0) continue;

                        double L = GetLength(Dx, Dy);
                        double d = GetLength(segment, Distance);

                        if (L > Math.Abs(Distance))
                        {
                            continue;
                        }

                        vec2 circlePoint = Points[i].GetProportionPoint(Math.Abs(angle) > Math.PI ? -d : d, L, Dx, Dy);
                        OffsetPoints.Add(new vec2(circlePoint.easting, circlePoint.northing));
                    }
                }
                else if (i == Start || i == End)//folow line
                {
                    int num = Start < End ? -1 : 1;
                    if (i == End) num *= -1;
                    if (Start > End) num *= -1;

                    int j = (i - num).Clamp(Points.Count);

                    Dx = Points[num > 0 ? i : j].northing - Points[num > 0 ? j : i].northing;
                    Dy = Points[num > 0 ? i : j].easting - Points[num > 0 ? j : i].easting;

                    Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                    vec2 Point = new vec2(Points[i].easting + Distance * (Dx / Length), Points[i].northing + Distance * (-Dy / Length));
                    vec2 Point2 = new vec2(Points[j].easting + Distance * (Dx / Length), Points[j].northing + Distance * (-Dy / Length));

                    if (GetLineIntersection(Point2, Point, Points[i], Points[(i + num).Clamp(Points.Count)], out vec2 Crossing, out _))
                        OffsetPoints.Add(Crossing);
                    else
                        OffsetPoints.Add(Point);
                }
                else if (Add)
                    OffsetPoints.Add(Points[i]);
            }
            return OffsetPoints;
        }

        public static double GetLength(double dx, double dy)
        {
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static vec2 GetProportionPoint(this vec2 point, double segment, double length, double dx, double dy)
        {
            double factor = segment / length;
            if (double.IsNaN(factor)) factor = 0;
            return new vec2(point.easting - dy * factor, point.northing - dx * factor);
        }

        public static List<Polyline> ClipPolyLine(this List<vec2> Points, List<vec2> clipPoints, bool Loop, bool ClipWinding = true)
        {
            List<Polyline> FinalPolyLine = new List<Polyline>();

            if (Points.Count < 2) return FinalPolyLine;

            VertexPoint First = new VertexPoint(Points[0]);
            VertexPoint CurrentVertex = First;

            for (int i = 1; i < Points.Count; i++)
            {
                CurrentVertex.Next = new VertexPoint(Points[i]);
                CurrentVertex = CurrentVertex.Next;
            }
            CurrentVertex.Next = First;

            List<VertexPoint> Crossings = new List<VertexPoint>();
            List<VertexPoint> Polygons = new List<VertexPoint>();

            CurrentVertex = First;
            VertexPoint StopVertex = CurrentVertex;

            int IntersectionCount = 0;

            int TotalCount = Points.Count;
            int safety = 0;

            bool start = true;
            while (true)
            {
                if (!start && (Loop ? CurrentVertex == StopVertex : CurrentVertex.Next == StopVertex)) break;
                start = false;

                VertexPoint SecondVertex = CurrentVertex.Next;

                List<VertexPoint> Crossings2 = new List<VertexPoint>();
                int safety2 = 0;
                bool start2 = true;
                while (true)
                {
                    if (!start2 && (Loop ? SecondVertex == StopVertex : SecondVertex.Next == StopVertex)) break;
                    start2 = false;

                    if (GetLineIntersection(CurrentVertex.Coords, CurrentVertex.Next.Coords, SecondVertex.Coords, SecondVertex.Next.Coords, out vec2 intersectionPoint2D, out double Time))
                    {
                        VertexPoint aa = new VertexPoint(intersectionPoint2D)
                        {
                            Crossing = CurrentVertex,
                            Next = SecondVertex,
                            Time = Time
                        };
                        Crossings2.Add(aa);

                        IntersectionCount++;
                    }
                    SecondVertex = SecondVertex.Next;

                    if (++safety2 - safety > TotalCount + IntersectionCount) break;
                }
                CurrentVertex = CurrentVertex.Next;

                Crossings2.Sort((x, y) => y.Time.CompareTo(x.Time));

                for (int j = 0; j < Crossings2.Count; j++)
                {
                    VertexPoint AA = InsertCrossing(Crossings2[j].Coords, Crossings2[j].Crossing);
                    VertexPoint BB = InsertCrossing(Crossings2[j].Coords, Crossings2[j].Next);

                    AA.Crossing = BB;
                    BB.Crossing = AA;
                }

                if (++safety > TotalCount) break;
            }

            TotalCount += IntersectionCount * 2;

            if (IntersectionCount > 0)
            {
                CurrentVertex = First;
                StopVertex = CurrentVertex;

                bool Searching = true;
                start = true;
                safety = 0;

                while (Crossings.Count > 0 || Searching)
                {
                    if (Crossings.Count > 0)
                    {
                        start = true;
                        CurrentVertex = Crossings[0];
                        StopVertex = CurrentVertex;
                        Crossings.RemoveAt(0);
                    }

                    while (true)
                    {
                        if (!start && CurrentVertex == StopVertex)
                        {
                            Polygons.Add(CurrentVertex);
                            Searching = false;
                            break;
                        }

                        start = false;
                        if (CurrentVertex.Crossing != null)
                        {
                            if (Loop) Crossings.Add(CurrentVertex.Next);
                            safety = 0;

                            VertexPoint CC = CurrentVertex.Crossing.Next;
                            CurrentVertex.Crossing.Next = CurrentVertex.Next;

                            //CurrentVertex.Next.Prev = CurrentVertex.Crossing;

                            CurrentVertex.Crossing.Crossing = null;
                            CurrentVertex.Next = CC;
                            //CurrentVertex.Next.Prev = CurrentVertex;
                            CurrentVertex.Crossing = null;
                        }
                        CurrentVertex = CurrentVertex.Next;
                        if (++safety > TotalCount) break;
                    }
                }
            }
            else Polygons.Add(First);

            if (!Loop)
            {
                for (int i = 0; i < Polygons.Count; i++)
                {
                    CurrentVertex = Polygons[i];
                    StopVertex = CurrentVertex;
                    bool isInside;
                    if (ClipWinding && clipPoints?.Count > 2)
                    {
                        isInside = clipPoints.PointInPolygon(CurrentVertex.Coords);
                    }
                    else
                        isInside = true;

                    if (isInside) FinalPolyLine.Add(new Polyline());

                    safety = 0;
                    start = true;
                    while (true)
                    {
                        if (isInside)
                        {
                            FinalPolyLine[FinalPolyLine.Count - 1].Points.Add(CurrentVertex.Coords);
                        }
                        if (!start && CurrentVertex.Next == StopVertex) break;
                        start = false;

                        if (clipPoints?.Count > 2)
                        {
                            List<vec3> Crossings2 = new List<vec3>();
                            int j = clipPoints.Count - 1;
                            for (int k = 0; k < clipPoints.Count; j = k++)
                            {
                                if (GetLineIntersection(CurrentVertex.Coords, CurrentVertex.Next.Coords, clipPoints[j], clipPoints[k], out vec2 Crossing, out double Time))
                                {
                                    Crossings2.Add(new vec3(Crossing.easting, Crossing.northing, Time));
                                }
                            }

                            if (Crossings2.Count > 0)
                            {
                                Crossings2.Sort((x, y) => x.heading.CompareTo(y.heading));

                                for (int k = 0; k < Crossings2.Count; k++)
                                {
                                    if (isInside && FinalPolyLine.Count > 0)
                                    {
                                        FinalPolyLine[FinalPolyLine.Count - 1].Points.Add(new vec2(Crossings2[k].easting, Crossings2[k].northing));
                                    }
                                    if (isInside = !isInside)
                                    {
                                        FinalPolyLine.Add(new Polyline());
                                        FinalPolyLine[FinalPolyLine.Count - 1].Points.Add(new vec2(Crossings2[k].easting, Crossings2[k].northing));
                                    }
                                }
                            }
                        }

                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > TotalCount) break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Polygons.Count; i++)
                {
                    FinalPolyLine.Add(new Polyline());
                    FinalPolyLine[i].Loop = true;
                    start = true;
                    CurrentVertex = Polygons[i];

                    StopVertex = CurrentVertex;
                    safety = 0;
                    while (true)
                    {
                        if (!start && CurrentVertex == StopVertex)
                            break;
                        start = false;

                        FinalPolyLine[i].Points.Add(CurrentVertex.Coords);

                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > TotalCount) break;
                    }
                }

                if (ClipWinding)
                {
                    for (int i = FinalPolyLine.Count - 1; i >= 0; i--)
                    {
                        if (FinalPolyLine[i].Points.Count < 3)
                        {
                            FinalPolyLine.RemoveAt(i);
                            continue;
                        }

                        double area = 0;
                        int o = FinalPolyLine[i].Points.Count - 1;
                        for (int l = 0; l < FinalPolyLine[i].Points.Count; o = l++)
                        {
                            area += (FinalPolyLine[i].Points[o].easting + FinalPolyLine[i].Points[l].easting) * (FinalPolyLine[i].Points[o].northing - FinalPolyLine[i].Points[l].northing);
                        }

                        if (area < 0)
                        {
                            FinalPolyLine.RemoveAt(i);
                            continue;
                        }
                        else
                            FinalPolyLine[i].area = area;




                        FinalPolyLine[i].winding = -255;
                        bool inside;
                        for (int j = 2; j < FinalPolyLine[i].Points.Count; j++)
                        {
                            if (!IsTriangleOrientedClockwise(FinalPolyLine[i].Points[j - 2], FinalPolyLine[i].Points[j - 1], FinalPolyLine[i].Points[j]))
                            {
                                inside = false;

                                for (int k = 0; k < FinalPolyLine.Count; k++)
                                {
                                    for (int l = 0; l < FinalPolyLine[k].Points.Count; l++)
                                    {
                                        if (IsPointInTriangle(FinalPolyLine[i].Points[j - 2], FinalPolyLine[i].Points[j - 1], FinalPolyLine[i].Points[j], FinalPolyLine[k].Points[l]))
                                        {
                                            inside = true;
                                            break;
                                        }
                                    }
                                    if (inside) break;
                                }
                                if (!inside)
                                {
                                    int winding_number = 0;

                                    double a = (FinalPolyLine[i].Points[j - 2].northing + FinalPolyLine[i].Points[j - 1].northing + FinalPolyLine[i].Points[j].northing) / 3.0;
                                    double b = (FinalPolyLine[i].Points[j - 2].easting + FinalPolyLine[i].Points[j - 1].easting + FinalPolyLine[i].Points[j].easting) / 3.0;

                                    vec2 test3 = new vec2(b, a);
                                    int l = Points.Count - 1;
                                    for (int m = 0; m < Points.Count; l = m++)
                                    {
                                        if (Points[l].easting <= test3.easting && Points[m].easting > test3.easting)
                                        {
                                            if ((Points[m].northing - Points[l].northing) * (test3.easting - Points[l].easting) -
                                            (test3.northing - Points[l].northing) * (Points[m].easting - Points[l].easting) > 0)
                                            {
                                                ++winding_number;
                                            }
                                        }
                                        else if (Points[l].easting > test3.easting && Points[m].easting <= test3.easting)
                                        {
                                            if ((Points[m].northing - Points[l].northing) * (test3.easting - Points[l].easting) -
                                            (test3.northing - Points[l].northing) * (Points[m].easting - Points[l].easting) < 0)
                                            {
                                                --winding_number;
                                            }
                                        }
                                    }
                                    FinalPolyLine[i].winding = winding_number;
                                    break;
                                }
                            }
                        }
                        if (FinalPolyLine[i].winding != 1)
                        {
                            FinalPolyLine.RemoveAt(i);
                            continue;
                        }
                    }
                    FinalPolyLine.Sort((x, y) => y.area.CompareTo(x.area));
                }
            }
            return FinalPolyLine;
        }

        public static void LangSimplify(this List<vec3> PointList, double Tolerance)
        {
            int key = 0;
            int endP = PointList.Count - 1;
            while (key < PointList.Count)
            {
                if (key + 1 == endP)
                {
                    key++;
                    endP = PointList.Count - 1;
                    continue;
                }
                else
                {
                    double maxD = 0;
                    for (int i = key + 1; i < endP; i++)
                    {
                        double d = PointList[i].FindDistanceToSegment(PointList[key], PointList[endP]);
                        if (d > maxD)
                        {
                            maxD = d;
                            if (d > Tolerance)
                            {
                                break;
                            }
                        }
                    }

                    if (maxD > Tolerance)
                        endP--;
                    else
                    {
                        for (int i = endP - 1; i > key; i--)
                            PointList.RemoveAt(i);
                        key++;
                        endP = PointList.Count - 1;
                    }
                }
            }
        }

        public static double FindDistanceToSegment(this vec3 pt, vec3 p1, vec3 p2)
        {
            double dx = p2.northing - p1.northing;
            double dy = p2.easting - p1.easting;
            if ((dx == 0) && (dy == 0))
            {
                dx = pt.northing - p1.northing;
                dy = pt.easting - p1.easting;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            double Time = ((pt.northing - p1.northing) * dx + (pt.easting - p1.easting) * dy) / (dx * dx + dy * dy);

            if (Time < 0)
            {
                dx = pt.northing - p1.northing;
                dy = pt.easting - p1.easting;
            }
            else if (Time > 1)
            {
                dx = pt.northing - p2.northing;
                dy = pt.easting - p2.easting;
            }
            else
            {
                dx = pt.northing - (p1.northing + Time * dx);
                dy = pt.easting - (p1.easting + Time * dy);
            }
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static List<int> TriangulatePolygon(this List<vec2> Points)
        {
            List<int> Indexer = new List<int>();
            if (Points.Count < 3) return Indexer;
            List<int> OldIdx = new List<int>();

            for (int i = 0; i < Points.Count; i++)
            {
                OldIdx.Add(i);
            }
            bool test = true;
            int j = 0;
            while (OldIdx.Count > 3)
            {
                if (j >= OldIdx.Count)
                {
                    if (test)
                    {
                        test = false;
                        j = 0;
                    }
                    else
                    {
                        //only happens on self crossing polygons!
                        break;
                    }
                }

                int i = j < 1 ? OldIdx.Count - 1 : j - 1;
                int k = j >= OldIdx.Count - 1 ? 0 : j + 1;

                if (IsEar(Points[OldIdx[i]], Points[OldIdx[j]], Points[OldIdx[k]], Points))
                {
                    test = true;
                    Indexer.Add(OldIdx[j]);
                    Indexer.Add(OldIdx[i]);
                    Indexer.Add(OldIdx[k]);

                    OldIdx.RemoveAt(j);
                }
                else j++;
            }

            if (IsEar(Points[OldIdx[0]], Points[OldIdx[1]], Points[OldIdx[2]], Points))
            {
                Indexer.Add(OldIdx[1]);
                Indexer.Add(OldIdx[0]);
                Indexer.Add(OldIdx[2]);
            }

            return Indexer;
        }

        public static bool IsTriangleOrientedClockwise(vec2 p1, vec2 p2, vec2 p3)
        {
            return p1.northing * p2.easting + p3.northing * p1.easting + p2.northing * p3.easting - p1.northing * p3.easting - p3.northing * p2.easting - p2.northing * p1.easting <= 0;
        }

        private static bool IsEar(vec2 PointA, vec2 PointB, vec2 PointC, List<vec2> vertices)
        {
            bool hasPointInside = IsTriangleOrientedClockwise(PointA, PointB, PointC);
            if (!hasPointInside)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (IsPointInTriangle(PointA, PointB, PointC, vertices[i]))
                    {
                        hasPointInside = true;
                        break;
                    }
                }
            }
            return !hasPointInside;
        }

        public static bool IsPointInTriangle(vec2 p1, vec2 p2, vec2 p3, vec2 p)
        {
            double Denominator = ((p2.easting - p3.easting) * (p1.northing - p3.northing) + (p3.northing - p2.northing) * (p1.easting - p3.easting));
            double a = ((p2.easting - p3.easting) * (p.northing - p3.northing) + (p3.northing - p2.northing) * (p.easting - p3.easting)) / Denominator;

            if (a > 0.0 && a < 1.0)
            {
                double b = ((p3.easting - p1.easting) * (p.northing - p3.northing) + (p1.northing - p3.northing) * (p.easting - p3.easting)) / Denominator;
                if (b > 0.0 && b < 1.0)
                {
                    double c = 1 - a - b;
                    if (c > 0.0 && c < 1.0)
                        return true;
                }
            }
            return false;
        }
    }
}
