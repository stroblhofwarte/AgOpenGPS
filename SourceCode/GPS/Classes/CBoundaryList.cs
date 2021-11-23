using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CBoundaryList
    {
        //list of coordinates of boundary line
        public Polygon fenceLine = new Polygon();
        public List<vec2> fenceLineEar = new List<vec2>(128);
        public Polygon hdLine = new Polygon();
        public List<vec3> turnLine = new List<vec3>(128);

        //constructor
        public CBoundaryList()
        {
            area = 0;
            isDriveThru = false;
        }
    }

    public class Polygon
    {
        public List<vec3> Points = new List<vec3>(128);
        public List<int> Indexer = new List<int>(384);

        public bool ResetPoints, ResetIndexer;
        public int BufferPoints = int.MinValue, BufferIndex = int.MinValue, BufferPointsCnt = 0, BufferIndexCnt = 0;

        public void DrawPolygon(bool Triangles)
        {
            if (Points.Count > 0)
            {
                if (Triangles && BufferIndex == int.MinValue || ResetIndexer || ResetPoints)
                {
                    double area = 0;
                    int j = Points.Count - 1;
                    for (int i = 0; i < Points.Count; j = i++)
                    {
                        area += (Points[i].northing - Points[j].northing) * (Points[i].easting + Points[j].easting);
                    }
                    if (area > 0)
                    {
                        Points.Reverse();//force Clockwise rotation
                    }

                    List<List<vec3>> rr = Points.ClipPolyLine(null, true, true);
                    if (rr.Count > 0)
                    {
                        rr.Sort((x, y) => y.Count.CompareTo(x.Count));
                        Points = rr[0];
                    }

                    double Area = Math.Abs(area / 2.0);

                    double Multiplier = Math.Max(1, Math.Min((Area / 10000) / 10000, 10));
                    double MinDist = 2 * Multiplier;
                    double distance;

                    int k = Points.Count - 1;
                    for (int l = 0; l < Points.Count; k = l++)
                    {
                        if (k < 0) k = Points.Count - 1;
                        //make sure distance isn't too small between points on turnLine
                        distance = glm.Distance(Points[k], Points[l]);
                        if (distance < MinDist)
                        {
                            Points.RemoveAt(l);
                            l--;
                        }
                    }

                    //lang simplification
                    Points.LangSimplify(0.05);
                    ResetPoints = true;

                    Indexer = Points.TriangulatePolygon();

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
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Points.Count * 24), Points.ToArray(), BufferUsageHint.StaticDraw);
                    BufferPointsCnt = Points.Count;
                    ResetPoints = false;
                }

                GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
                GL.VertexPointer(3, VertexPointerType.Double, 0, IntPtr.Zero);
                GL.EnableClientState(ArrayCap.VertexArray);

                if (Triangles)
                {
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                    GL.DrawElements(PrimitiveType.Triangles, BufferIndexCnt, DrawElementsType.UnsignedInt, IntPtr.Zero);
                }
                else
                {
                    GL.DrawArrays(PrimitiveType.LineLoop, 0, BufferPointsCnt);
                }
            }
        }
    }

    public static class StaticClass
    {
        public class VertexPoint
        {
            public vec3 Coords;
            public VertexPoint Next;
            public VertexPoint Prev;
            public VertexPoint Crossing;
            //ClockWise or Crossing;
            public bool Data = false;
            public double Time = -1;

            public VertexPoint(vec3 coords, bool intersection = false)
            {
                Coords = coords;
                Data = intersection;
            }
        }

        public static List<VertexPoint> PolyLineStructure(List<vec3> polyLine)
        {
            List<VertexPoint> PolyLine = new List<VertexPoint>();

            for (int i = 0; i < polyLine.Count; i++)
            {
                PolyLine.Add(new VertexPoint(polyLine[i], false));
            }

            for (int i = 0; i < PolyLine.Count; i++)
            {
                int Next = (i + 1).Clamp(PolyLine.Count);
                int Prev = (i - 1).Clamp(PolyLine.Count);

                PolyLine[i].Next = PolyLine[Next];
                PolyLine[i].Prev = PolyLine[Prev];
            }

            return PolyLine;
        }
        public static int Clamp(this int Idx, int Size)
        {
            return (Size + Idx) % Size;
        }
        public static bool GetLineIntersection(vec3 PointAA, vec3 PointAB, vec3 PointBA, vec3 PointBB, out vec3 Crossing, out double TimeA, bool Limit = false)
        {
            TimeA = -1;
            Crossing = new vec3();
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

        public static VertexPoint InsertCrossing(vec3 intersectionPoint, VertexPoint currentVertex)
        {
            VertexPoint IntersectionCrossing = new VertexPoint(intersectionPoint, true)
            {
                Next = currentVertex.Next,
                Prev = currentVertex
            };
            currentVertex.Next.Prev = IntersectionCrossing;
            currentVertex.Next = IntersectionCrossing;
            return IntersectionCrossing;
        }

        public static bool PointInPolygon(this List<vec3> Polygon, vec3 pointAA)
        {
            vec3 PointAB = new vec3(0.0, 200000.0, 0.0);

            int NumCrossings = 0;

            for (int i = 0; i < Polygon.Count; i++)
            {
                vec3 PointBB = Polygon[(i + 1).Clamp(Polygon.Count)];

                if (GetLineIntersection(pointAA, PointAB, Polygon[i], PointBB, out _, out _))
                    NumCrossings += 1;
            }
            return NumCrossings % 2 == 1;
        }

        public static List<List<vec3>> ClipPolyLine(this List<vec3> Points, List<vec3> clipPoints, bool Loop, bool ClipWinding = true)
        {
            List<List<vec3>> FinalPolyLine = new List<List<vec3>>();
            List<VertexPoint> PolyLine = PolyLineStructure(Points);

            List<VertexPoint> Crossings = new List<VertexPoint>();
            List<VertexPoint> Polygons = new List<VertexPoint>();
            if (PolyLine.Count < 2) return FinalPolyLine;
            VertexPoint CurrentVertex = PolyLine[0];
            VertexPoint StopVertex;
            if (Loop) StopVertex = CurrentVertex;
            else StopVertex = CurrentVertex.Prev;

            int IntersectionCount = 0;
            int safety = 0;
            bool start = true;
            while (true)
            {
                if (!start && CurrentVertex == StopVertex) break;
                start = false;

                VertexPoint SecondVertex = CurrentVertex.Next;

                List<VertexPoint> Crossings2 = new List<VertexPoint>();
                int sectcnt = 0;
                int safety2 = 0;
                bool start2 = true;
                while (true)
                {
                    if (!start2 && SecondVertex == StopVertex) break;
                    start2 = false;

                    if (GetLineIntersection(CurrentVertex.Coords, CurrentVertex.Next.Coords, SecondVertex.Coords, SecondVertex.Next.Coords, out vec3 intersectionPoint2D, out double Time))
                    {
                        VertexPoint aa = new VertexPoint(intersectionPoint2D);
                        aa.Prev = CurrentVertex;
                        aa.Next = SecondVertex;
                        aa.Time = Time;
                        Crossings2.Add(aa);

                        sectcnt++;
                        IntersectionCount++;
                    }
                    SecondVertex = SecondVertex.Next;

                    if (safety2++ > PolyLine.Count * 1.2) break;
                }
                CurrentVertex = CurrentVertex.Next;

                Crossings2.Sort((x, y) => y.Time.CompareTo(x.Time));

                for (int j = 0; j < Crossings2.Count; j++)
                {
                    VertexPoint AA = InsertCrossing(Crossings2[j].Coords, Crossings2[j].Prev);
                    VertexPoint BB = InsertCrossing(Crossings2[j].Coords, Crossings2[j].Next);

                    AA.Crossing = BB;
                    BB.Crossing = AA;
                }

                if (safety++ > PolyLine.Count * 1.2) break;
            }
            if (IntersectionCount > 0)
            {
                CurrentVertex = PolyLine[0];
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
                        if (CurrentVertex.Data)
                        {
                            if (Loop) Crossings.Add(CurrentVertex.Next);
                            safety = 0;
                            VertexPoint CC = CurrentVertex.Crossing.Next;
                            CurrentVertex.Crossing.Next = CurrentVertex.Next;
                            CurrentVertex.Next.Prev = CurrentVertex.Crossing;
                            CurrentVertex.Crossing.Data = false;
                            CurrentVertex.Crossing.Crossing = null;
                            CurrentVertex.Next = CC;
                            CurrentVertex.Next.Prev = CurrentVertex;
                            CurrentVertex.Data = false;
                            CurrentVertex.Crossing = null;
                        }
                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > PolyLine.Count * 1.2) break;
                    }
                }
            }
            else Polygons.Add(PolyLine[0]);

            if (!Loop)
            {
                for (int i = 0; i < Polygons.Count; i++)
                {
                    CurrentVertex = Polygons[i];
                    StopVertex = CurrentVertex.Prev;
                    bool isInside;
                    if (ClipWinding && clipPoints?.Count > 2)
                    {
                        isInside = clipPoints.PointInPolygon(CurrentVertex.Coords);
                    }
                    else
                        isInside = true;

                    if (isInside) FinalPolyLine.Add(new List<vec3>());

                    safety = 0;
                    start = true;
                    while (true)
                    {
                        if (isInside)
                        {
                            FinalPolyLine[FinalPolyLine.Count - 1].Add(CurrentVertex.Coords);
                        }
                        if (!start && CurrentVertex == StopVertex) break;
                        start = false;

                        if (clipPoints?.Count > 2)
                        {
                            List<vec3> Crossings2 = new List<vec3>();
                            int j = clipPoints.Count - 1;
                            for (int k = 0; k < clipPoints.Count; j = k++)
                            {
                                if (GetLineIntersection(CurrentVertex.Coords, CurrentVertex.Next.Coords, clipPoints[j], clipPoints[k], out vec3 Crossing, out double Time))
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
                                        FinalPolyLine[FinalPolyLine.Count - 1].Add(new vec3(Crossings2[k].easting, Crossings2[k].northing, 0));
                                    }
                                    if (isInside = !isInside)
                                    {
                                        FinalPolyLine.Add(new List<vec3>());
                                        FinalPolyLine[FinalPolyLine.Count - 1].Add(new vec3(Crossings2[k].easting, Crossings2[k].northing, 0));
                                    }
                                }
                            }
                        }

                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > PolyLine.Count * 1.2) break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Polygons.Count; i++)
                {
                    FinalPolyLine.Add(new List<vec3>());

                    start = true;
                    CurrentVertex = Polygons[i];

                    if (Loop) StopVertex = CurrentVertex;
                    else StopVertex = CurrentVertex.Prev;
                    safety = 0;
                    while (true)
                    {
                        if (!start && CurrentVertex == StopVertex)
                            break;
                        start = false;

                        FinalPolyLine[i].Add(CurrentVertex.Coords);

                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > PolyLine.Count) break;
                    }
                }

                if (ClipWinding)
                {
                    int[] Windings = new int[FinalPolyLine.Count];

                    for (int i = 0; i < FinalPolyLine.Count; i++)
                    {
                        Windings[i] = -1000;
                        bool inside;
                        for (int j = 2; j < FinalPolyLine[i].Count; j++)
                        {
                            if (!IsTriangleOrientedClockwise(FinalPolyLine[i][j - 2], FinalPolyLine[i][j - 1], FinalPolyLine[i][j]))
                            {
                                inside = false;

                                for (int k = 0; k < FinalPolyLine.Count; k++)
                                {
                                    for (int l = 0; l < FinalPolyLine[k].Count; l++)
                                    {
                                        if (IsPointInTriangle(FinalPolyLine[i][j - 2], FinalPolyLine[i][j - 1], FinalPolyLine[i][j], FinalPolyLine[k][l]))
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

                                    double a = (FinalPolyLine[i][j - 2].northing + FinalPolyLine[i][j - 1].northing + FinalPolyLine[i][j].northing) / 3.0;
                                    double b = (FinalPolyLine[i][j - 2].easting + FinalPolyLine[i][j - 1].easting + FinalPolyLine[i][j].easting) / 3.0;

                                    vec2 test3 = new vec2(b, a);

                                    for (int k = 0; k < FinalPolyLine.Count; k++)
                                    {
                                        int l = FinalPolyLine[k].Count - 1;
                                        for (int m = 0; m < FinalPolyLine[k].Count; l = m++)
                                        {
                                            if (FinalPolyLine[k][l].easting <= test3.easting && FinalPolyLine[k][m].easting > test3.easting)
                                            {
                                                if ((FinalPolyLine[k][m].northing - FinalPolyLine[k][l].northing) * (test3.easting - FinalPolyLine[k][l].easting) -
                                                (test3.northing - FinalPolyLine[k][l].northing) * (FinalPolyLine[k][m].easting - FinalPolyLine[k][l].easting) > 0)
                                                {
                                                    ++winding_number;
                                                }
                                            }
                                            else
                                            {
                                                if (FinalPolyLine[k][l].easting > test3.easting && FinalPolyLine[k][m].easting <= test3.easting)
                                                {
                                                    if ((FinalPolyLine[k][m].northing - FinalPolyLine[k][l].northing) * (test3.easting - FinalPolyLine[k][l].easting) -
                                                    (test3.northing - FinalPolyLine[k][l].northing) * (FinalPolyLine[k][m].easting - FinalPolyLine[k][l].easting) < 0)
                                                    {
                                                        --winding_number;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    Windings[i] = winding_number;
                                    break;
                                }
                            }
                        }
                    }

                    for (int i = FinalPolyLine.Count - 1; i >= 0; i--)
                    {
                        int aa = Windings[i];
                        if (Windings[i] != 1)
                            FinalPolyLine.RemoveAt(i);
                    }
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

        public static List<int> TriangulatePolygon(this List<vec3> Points)
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

        public static bool IsTriangleOrientedClockwise(vec3 p1, vec3 p2, vec3 p3)
        {
            return p1.northing * p2.easting + p3.northing * p1.easting + p2.northing * p3.easting - p1.northing * p3.easting - p3.northing * p2.easting - p2.northing * p1.easting <= 0;
        }

        private static bool IsEar(vec3 PointA, vec3 PointB, vec3 PointC, List<vec3> vertices)
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

        public static bool IsPointInTriangle(vec3 p1, vec3 p2, vec3 p3, vec3 p)
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