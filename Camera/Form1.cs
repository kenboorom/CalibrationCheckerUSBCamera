using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Threading;
using System.IO;

using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Camera
{ 
    public partial class Form1 : Form
    {
        private FilterInfoCollection VideoCaptureDevices;
        private VideoCaptureDevice FinalVideo;
        int frameCount = 0;

        StreamWriter myTraceFile;

        Bitmap ReferenceImageFromResources;

        public Form1() // init
        {
            InitializeComponent();
            {
                VideoCaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                foreach (FilterInfo VideoCaptureDevice in VideoCaptureDevices)
                {
                    comboBox1.Items.Add(VideoCaptureDevice.Name);
                }
                comboBox1.SelectedIndex = 1;
            }

            /*myTraceFile.AutoFlush = true;
            TextWriterTraceListener myTextListener = new
               TextWriterTraceListener(myFile);
            myTraceFile.Listeners.Add(myTextListener);
            */

            //System.Reflection.Assembly myAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            //Stream myStream = myAssembly.GetManifestResourceStream("ReferenceCableImage.bmp");
            //ReferenceImageFromResources = new Bitmap(myStream);
             myTraceFile= new StreamWriter(@"C:\TEMP\CAMERA_myTraceFile.TXT");
             myTraceFile.AutoFlush = true;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            FinalVideo = new VideoCaptureDevice(VideoCaptureDevices[comboBox1.SelectedIndex].MonikerString);
            FinalVideo.NewFrame += new NewFrameEventHandler(FinalVideo_NewFrame);
            FinalVideo.Start();
        }


        /* Instructions for embedding image in resources
         * 
         * https://msdn.microsoft.com/en-us/library/aa984367(v=vs.71).aspx
         * 1. On the Project menu, choose Add Existing Item.
         * 2. Navigate to the image you want to add to your project. Click the Open button to add the image to your project's file list.
         * 3. Right-click the image in your project's file list and choose Properties. The Properties window appears.
         * 4. Find the Build Action property in the Properties window. Change its value to Embedded Resource.
         * 
         * 
         * 
         * */

        Bitmap originalFrame;

        void FinalVideo_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // Keep a copy of the original frame for later processing
            originalFrame = (Bitmap)eventArgs.Frame.Clone();

            // Make a local copy that we will use to annotate
            Bitmap newFrame = (Bitmap) originalFrame.Clone();
            frameCount++;

            // Assume height is smaller variable
            int circleSize = newFrame.Height - 20;
            int circleXOffset = (newFrame.Width - circleSize) / 2;
            int circleYOffset = 10;
    
            using (Graphics g = Graphics.FromImage(newFrame))
            {
                using (Pen b = new Pen(Color.Red, 5))
                {
                    g.DrawEllipse(b, circleXOffset, circleYOffset, circleSize, circleSize);
                }
            }
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.Image = newFrame;
            //textBox1.Text = $"{frameCount}";
            //convolveResult.Text = $"{frameCount}";

            // Try convolving with reference image
        }

        class LineCoord
        {
            public int startX;
            public int startY;
            public int endX;
            public int endY;

            public LineCoord(int passedStartX, int passedStartY, int passedEndX, int passedEndY)
            {
                startX = passedStartX;
                startY = passedStartY;
                endX = passedEndX;
                endY = passedEndY;
            }
        }

        // Return a count of all pixels in a vertical or horizontal line with intensity greater than targetValue.  Intensity 
        // is defined as the sum of all 3 RGB values.  This routine handles general case of a horizontal or veritcal line, defined
        // by points (startX, startY) and (endX, endY)
        private int CountIntensityGreaterThan(Bitmap b, LineCoord line, int targetValue)
        {
            int totalCount = 0;
            Color theColor;
            int theIntensity;

            myTraceFile.WriteLine($"Counting intensity along {line.startX},{line.startY} to {line.endX},{line.endY}");

            // Han
            int incrementX = Math.Sign(line.endX - line.startX);
            int incrementY = Math.Sign(line.endY - line.startY);

            // Handle horizontal line because we are changing the X as we move along, but keeping Y constant
            if (incrementX != 0)
            {
                myTraceFile.WriteLine("Counting intensity of horizontal line...");
                for (int currentX = line.startX; currentX != line.endX; currentX++)
                {
                    theColor = b.GetPixel(currentX, line.startY);
                    theIntensity = theColor.R + theColor.G + theColor.B;
                    if (theIntensity >= targetValue) totalCount++;
                    //myTraceFile.Write($"[{theColor.R:D3}, {theColor.G:D3}, {theColor.B:D3}, {totalCount:D4}], ");
                }
                //myTraceFile.WriteLine("");

            }
            else if (incrementY != 0)
            {
                myTraceFile.WriteLine("Counting intensity of vertical line...");

                for (int currentY = line.startY; currentY != line.endY; currentY++)
                {
                    theColor = b.GetPixel(line.startX, currentY);
                    theIntensity = theColor.R + theColor.G + theColor.B;
                    if (theIntensity >= targetValue) totalCount++;
                    //myTraceFile.Write($"[{theColor.R:D3}, {theColor.G:D3}, {theColor.B:D3}, {totalCount:D4}], ");
                    //myTraceFile.WriteLine("");
                }

            }
            else
                 throw new ArgumentException("Can't count intenesity when no lone is defined, starting&ending same");

            return totalCount;
        }

        // Takes input as a line (startX, startY) to (endX, endY).  It moves the line up/down/left/right until there are at least
        // requireCount pixels on the line with intensity at or exceeding requireThreshold.  Returns 1 for success, 0 for failure.
        private int FindALine(Bitmap b, LineCoord line, int incrementX, int incrementY, int requireCount, int requireThreshold)
        {
            int maximumX = b.Width - 1;
            int maximumY = b.Height - 1;

            myTraceFile.WriteLine("Start FindALine");
            while ( (line.startX >= 0) && (line.startY >= 0) && (line.startX <= maximumX) && (line.startY <= maximumY) )
            {
                int myCount = CountIntensityGreaterThan(b, line, requireThreshold);
                if (myCount >= requireCount)
                {
                    myTraceFile.WriteLine($"FindALine: Found line, coordinates={line.startX},{line.startY}  to {line.endX},{line.endY}");
                    return 1;
                }
                line.startX += incrementX;
                line.startY += incrementY;
                line.endX += incrementX;
                line.endY += incrementY;
            }

            myTraceFile.WriteLine("Unable to find line meeting requirments");
            // We went out of bounds - did not find target
            return 0;

        }

        private void DrawLine(Bitmap b, LineCoord l, Color targetColor)
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                using (Pen myPen = new Pen(targetColor, 5))
                {
                    System.Drawing.Point P1 = new System.Drawing.Point(l.startX, l.startY);
                    System.Drawing.Point P2 = new System.Drawing.Point(l.endX, l.endY);

                    g.DrawLine(myPen, P1, P2);
                }
            }
        }

        void JustDrawBoundingBox(Bitmap b, LineCoord boundingBox, Color targetColor)
        {
            // SOUTH LINE: At bottom of page.  Has maximumY.  Delta Y is negative (move it up)
            LineCoord southLine = new LineCoord(boundingBox.startX, boundingBox.endY, boundingBox.endX, boundingBox.endY);
            // DrawLine(n, southLine);
                DrawLine(b, southLine, targetColor);

            // NORTH LINE: At top of page.  Has minimum .  Delta Y is positive (move it down)
            LineCoord northLine = new LineCoord(boundingBox.startX, boundingBox.startY, boundingBox.endX, boundingBox.startY);
                DrawLine(b, northLine, targetColor);


            LineCoord eastLine = new LineCoord(boundingBox.endX, boundingBox.startY, boundingBox.endX, boundingBox.endY);
                DrawLine(b, eastLine, targetColor);

            LineCoord westLine = new LineCoord(boundingBox.startX, boundingBox.startY, boundingBox.startX, boundingBox.endY);
                DrawLine(b, westLine, targetColor);

        }

        void FindDrawBoundingBox(Bitmap b, LineCoord boundingBox, Color targetColor, int askThreshold, int askCount)
        {
            int okReturn;

            // SOUTH LINE: At bottom of page.  Has maximumY.  Delta Y is negative (move it up)
            LineCoord southLine = new LineCoord(boundingBox.startX, boundingBox.endY, boundingBox.endX, boundingBox.endY);
            // DrawLine(b, southLine);
            okReturn = FindALine(b, southLine, 0, -1, askCount, askThreshold);
            if (okReturn == 1)
                DrawLine(b, southLine, targetColor);

            // NORTH LINE: At top of page.  Has minimum .  Delta Y is positive (move it down)
            LineCoord northLine = new LineCoord(boundingBox.startX, boundingBox.startY, boundingBox.endX, boundingBox.startY);
            okReturn = FindALine(b, northLine, 0, 1, askCount, askThreshold);
            if (okReturn == 1)
                DrawLine(b, northLine, targetColor);


            LineCoord eastLine = new LineCoord(boundingBox.endX, boundingBox.startY, boundingBox.endX, boundingBox.endY);
            okReturn = FindALine(b, eastLine, -1, 0, askCount, askThreshold);
            if (okReturn == 1)
                DrawLine(b, eastLine, targetColor);

            LineCoord westLine = new LineCoord(boundingBox.startX, boundingBox.startY, boundingBox.startX, boundingBox.endY);
            okReturn = FindALine(b, westLine, +1, 0, askCount, askThreshold);
            if (okReturn == 1)
                DrawLine(b, westLine, targetColor);

            boundingBox.startX = westLine.startX;
            boundingBox.startY = northLine.startY;
            boundingBox.endX = eastLine.endX;
            boundingBox.endY = southLine.endY;
        }

        private void AddHistogram(int[] histogram, Bitmap b, LineCoord region, int t, Bitmap annotate)
        {
            Color thePixel;
            for (int y=region.startY; y<=region.endY; y++)
              for (int x=region.startX; x<=region.endX; x++)
                {
                    thePixel = b.GetPixel(x, y);
                    int intensity =  thePixel.G + thePixel.B;
                    histogram[intensity]++;
                    //b.SetPixel(x, y, Color.Cyan);
                    if (intensity >= t)
                    {
                        annotate.SetPixel(x, y, Color.Violet);
                    }
                }
        }

        private void button2_Click(object sender, EventArgs e)
        {

            FinalVideo.Stop();

            // We need a new copy to annotate!!
            Bitmap annotatedFrame = new Bitmap(originalFrame);
            
            // Save original image to a disk file
            annotatedFrame.Save(@"C:\TEMP\TEST00.JPG");

            // Save the annotated image to a disk file
            pictureBox1.Image.Save(@"C:\TEMP\TEST01.JPG");

            int frameMaximumX = originalFrame.Width - 1;
            int frameMaxmiumY = originalFrame.Height - 1;

            // Draw outer bounding box
            LineCoord outerBound = new LineCoord(20, 20, frameMaximumX-50, frameMaxmiumY-50);

            FindDrawBoundingBox(annotatedFrame, outerBound, Color.Green, 450, 20);
            annotatedFrame.Save(@"C:\TEMP\TEST02.JPG");

            // Draw new bounding box
            JustDrawBoundingBox(annotatedFrame, outerBound, Color.Blue);
            annotatedFrame.Save(@"C:\TEMP\TEST03.JPG");

            // Draw middle bounding box
            LineCoord middleBound = new LineCoord(outerBound.startX + 80, outerBound.startY +60, outerBound.endX - 80, outerBound.endY - 70);
            JustDrawBoundingBox(annotatedFrame, middleBound, Color.Brown);
            annotatedFrame.Save(@"C:\TEMP\TEST04.JPG");

            // Find inner bounding box
            LineCoord innerBound = new LineCoord(middleBound.startX, middleBound.startY, middleBound.endX, middleBound.endY);
            FindDrawBoundingBox(annotatedFrame, innerBound, Color.Yellow, 600, 25);
            annotatedFrame.Save(@"C:\TEMP\TEST05.JPG");

            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.Image = (Image)annotatedFrame;


            // Calculate intensity histogram of pixels between outerBound and innerBound
            int maxIntensity = 256 * 3;
            int threshold = 260;
            int[] myHistogram = new int[maxIntensity+1];

            // TODO - REDO THIS SO WE USE CONCENTRIC CIRCLES!!
            LineCoord northBox = new LineCoord(middleBound.startX+10, middleBound.startY+10, middleBound.endX-3, innerBound.startY-3);
            LineCoord southBox = new LineCoord(middleBound.startX+3, innerBound.endY+3, middleBound.endX-10, middleBound.endY-10);

            LineCoord westBox = new LineCoord(middleBound.startX+3, innerBound.startY+3, innerBound.startX-10, innerBound.endY-10);

            LineCoord eastBox = new LineCoord(innerBound.endX+3, innerBound.startY+3, middleBound.endX-3, innerBound.endY-3);

            AddHistogram(myHistogram, originalFrame, northBox, threshold, annotatedFrame);
            AddHistogram(myHistogram, originalFrame, southBox, threshold, annotatedFrame);
            //AddHistogram(myHistogram, originalFrame, eastBox, threshold, annotatedFrame);
            //AddHistogram(myHistogram, originalFrame, westBox, threshold, annotatedFrame);

            originalFrame.Save(@"C:\TEMP\TEST_ORIGINAL_FRAME.JPG");
            // Count values greater than 438
            int runningTotal = 0;
            for (int h = threshold; h <= maxIntensity; h++)
                runningTotal += myHistogram[h];

            convolveResult.Text = $"{runningTotal}";
            progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            if (runningTotal < 50)
            {
                progressBar1.ForeColor = Color.Green;
                progressBar1.Value = 100;
                return;
            }

            if (runningTotal < 450)
            {
                progressBar1.ForeColor = Color.Yellow;
                progressBar1.Value = 50;
                return;
            }

            progressBar1.ForeColor = Color.Red;
            progressBar1.Value = 10;
            return;

            // How many?
            // ModifyProgressBarColor.SetState(progressBar1, 2);

            // Define four rectangular areas


            // Draw vertical and horizontal lines to box in the image.  
            // NET puts the origin in the UPPER LEFT CORNER.
            // NET uses Cartesian coordinates in I quadrante 
            // (0,0) is lower left.


        }

        private void buttonBuy_Click(object sender, EventArgs e)
        {
            Process.Start("IExplore.exe", "www.tek.com/tekstore/configure/P7625");
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
