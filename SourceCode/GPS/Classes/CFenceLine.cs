using System;

namespace AgOpenGPS
{
    public partial class CBoundaryList
    {
        //area variable
        public double area;

        //boundary variables
        public bool isDriveThru;

        public void CalculateFenceLineHeadings()
        {
            //to calc heading based on next and previous points to give an average heading.
            int cnt = fenceLine.Points.Count;
            vec3[] arr = new vec3[cnt];
            cnt--;
            fenceLine.Points.CopyTo(arr);
            fenceLine.Points.Clear();

            //first point needs last, first, second points
            vec3 pt3 = arr[0];
            pt3.heading = Math.Atan2(arr[1].easting - arr[cnt].easting, arr[1].northing - arr[cnt].northing);
            if (pt3.heading < 0) pt3.heading += glm.twoPI;
            fenceLine.Points.Add(pt3);

            //middle points
            for (int i = 1; i < cnt; i++)
            {
                pt3 = arr[i];
                pt3.heading = Math.Atan2(arr[i + 1].easting - arr[i - 1].easting, arr[i + 1].northing - arr[i - 1].northing);
                if (pt3.heading < 0) pt3.heading += glm.twoPI;
                fenceLine.Points.Add(pt3);
            }

            //last and first point
            pt3 = arr[cnt];
            pt3.heading = Math.Atan2(arr[0].easting - arr[cnt - 1].easting, arr[0].northing - arr[cnt - 1].northing);
            if (pt3.heading < 0) pt3.heading += glm.twoPI;
            fenceLine.Points.Add(pt3);
        }

        public void FixFenceLine(int bndNum)
        {
            double spacing;
            //boundary point spacing based on eq width
            //close if less then 30 ha, 60ha, more then 60
            if (area < 200000) spacing = 1.1;
            else if (area < 400000) spacing = 2.2;
            else spacing = 3.3;

            if (bndNum > 0) spacing *= 0.5;

            int bndCount = fenceLine.Points.Count;
            double distance;

            //make sure distance isn't too big between points on boundary
            for (int i = 0; i < bndCount; i++)
            {
                int j = i + 1;

                if (j == bndCount) j = 0;
                distance = glm.Distance(fenceLine.Points[i], fenceLine.Points[j]);
                if (distance > spacing * 1.5)
                {
                    vec3 pointB = new vec3((fenceLine.Points[i].easting + fenceLine.Points[j].easting) / 2.0,
                        (fenceLine.Points[i].northing + fenceLine.Points[j].northing) / 2.0, fenceLine.Points[i].heading);

                    fenceLine.Points.Insert(j, pointB);
                    bndCount = fenceLine.Points.Count;
                    i--;
                }
            }

            //make sure distance isn't too big between points on boundary
            bndCount = fenceLine.Points.Count;

            for (int i = 0; i < bndCount; i++)
            {
                int j = i + 1;

                if (j == bndCount) j = 0;
                distance = glm.Distance(fenceLine.Points[i], fenceLine.Points[j]);
                if (distance > spacing * 1.6)
                {
                    vec3 pointB = new vec3((fenceLine.Points[i].easting + fenceLine.Points[j].easting) / 2.0,
                        (fenceLine.Points[i].northing + fenceLine.Points[j].northing) / 2.0, fenceLine.Points[i].heading);

                    fenceLine.Points.Insert(j, pointB);
                    bndCount = fenceLine.Points.Count;
                    i--;
                }
            }

            //make sure distance isn't too small between points on headland
            spacing *= 1.2;
            bndCount = fenceLine.Points.Count;
            for (int i = 0; i < bndCount - 1; i++)
            {
                distance = glm.Distance(fenceLine.Points[i], fenceLine.Points[i + 1]);
                if (distance < spacing)
                {
                    fenceLine.Points.RemoveAt(i + 1);
                    bndCount = fenceLine.Points.Count;
                    i--;
                }
            }

            //make sure headings are correct for calculated points
            CalculateFenceLineHeadings();

            double delta = 0;
            fenceLineEar?.Clear();

            for (int i = 0; i < fenceLine.Points.Count; i++)
            {
                if (i == 0)
                {
                    fenceLineEar.Add(new vec2(fenceLine.Points[i].easting, fenceLine.Points[i].northing));
                    continue;
                }
                delta += (fenceLine.Points[i - 1].heading - fenceLine.Points[i].heading);
                if (Math.Abs(delta) > 0.01)
                {
                    fenceLineEar.Add(new vec2(fenceLine.Points[i].easting, fenceLine.Points[i].northing));
                    delta = 0;
                }
            }
        }

        public void ReverseWinding()
        {
            //reverse the boundary
            int cnt = fenceLine.Points.Count;
            vec3[] arr = new vec3[cnt];
            cnt--;
            fenceLine.Points.CopyTo(arr);
            fenceLine.Points.Clear();
            for (int i = cnt; i >= 0; i--)
            {
                arr[i].heading -= Math.PI;
                if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                fenceLine.Points.Add(arr[i]);
            }
        }

        //obvious
        public bool CalculateFenceArea(int idx)
        {
            int ptCount = fenceLine.Points.Count;
            if (ptCount < 1) return false;

            area = 0;         // Accumulates area in the loop
            int j = ptCount - 1;  // The last vertex is the 'previous' one to the first

            for (int i = 0; i < ptCount; j = i++)
            {
                area += (fenceLine.Points[j].easting + fenceLine.Points[i].easting) * (fenceLine.Points[j].northing - fenceLine.Points[i].northing);
            }

            bool isClockwise = area >= 0;

            area = Math.Abs(area / 2);

            //make sure is clockwise for outer counter clockwise for inner
            if ((idx == 0 && isClockwise) || (idx > 0 && !isClockwise))
            {
                ReverseWinding();
            }

            return isClockwise;
        }
    }
}
