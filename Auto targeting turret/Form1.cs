using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using AForge;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Imaging.Filters;


namespace Auto_targeting_turret
{
    public partial class Form1 : Form
    {
        public Stopwatch watch { get; set; }
        public Stopwatch shoot_watch { get; set; }
        public ColorFiltering RedFilter = new ColorFiltering();

        public Form1()
        {
            InitializeComponent();
        }

        FilterInfoCollection filterInfoCollection;
        VideoCaptureDevice videoCaptureDevice;

        private void btnStart_Click(object sender, EventArgs e)
        {
            shoot_watch = Stopwatch.StartNew();
            videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cboCamera.SelectedIndex - 1].MonikerString);
            videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
            videoCaptureDevice.Start();
        }


        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // creates a bitmap that is 200 by 200 pixels
            Bitmap img = new Bitmap((Bitmap)eventArgs.Frame.Clone(), 200, 200);
            int HEIGHT = img.Height;
            int WIDTH = img.Width;

            RedFilter.Red = new IntRange(100, 255);
            RedFilter.Green = new IntRange(0, 80);
            RedFilter.Blue = new IntRange(0, 80);

            Bitmap R_img = RedFilter.Apply(img);

            // stores the x and y indexes of all of the pixels in the img that are above a certain amount of redness
            List<(int, int)> reddest_pixels_list = new List<(int, int)>();
            int red_x = 0;
            int red_y = 0;

            // for loop that iterates through every pixel in the image and adds it to the reddest pixels list if it is red
            for (int i = 0; i < WIDTH; i++)
            {
                for (int j = 0; j < HEIGHT; j++)
                {
                    Color c = R_img.GetPixel(i, j);

                    if (c.R != 0)
                    {
                        reddest_pixels_list.Add((i, j));
                    }
                    else
                    {
                        Color c_temp = img.GetPixel(i, j);
                        int avg = (c_temp.R + c_temp.G + c_temp.B) / 3;
                        img.SetPixel(i, j, Color.FromArgb(c_temp.A, avg, avg, avg));
                    }
                }
            }

            // finds the x and y total of the red pixels, used to find the average x and y value
            int red_x_total = 0;
            int red_y_total = 0;
            for (int i = 0; i < reddest_pixels_list.Count; i++)
            {
                red_x_total += reddest_pixels_list[i].Item1;
                red_y_total += reddest_pixels_list[i].Item2;
            }


            // I did some testing to find the offsets and used a variable to make it easier to change
            int middle_x = WIDTH / 2 - 2;
            int middle_y = HEIGHT / 2 + 70;

            bool fire = true;
            // the turret doesn't move if there aren't any pixels that are red enough
            if (reddest_pixels_list.Count == 0)
            {
                red_x = middle_x;
                red_y = middle_y;
                fire = false;
            }
            else
            {
                // if the turret does find pixels that are red enough, this is where it sets the values for red_x and red_y, which are the average values of all te red pixels
                red_x = red_x_total / reddest_pixels_list.Count;
                red_y = red_y_total / reddest_pixels_list.Count;
            }

            // write the red_x and red_y to the terminal
            Console.WriteLine(string.Format("Red_x: {0}\nRed_y: {1}", red_x, red_y));

            // the delta_x and delta_y values are changed based off where red_x and red_y values are in relation to where the pvc is aiming
            int delta_x = 0;
            int delta_y = 0;

            // the if statements first check whether red_x is to the left or to the right of where the pvc is aiming
            if (red_x < middle_x)
            {
                // then it checks to see if red_x is within 50 pixels of the middle
                // if it is not within 50 pixels, it will move the turret two degrees, and if it is within 50 pixels but not within 10 pixels, it will move the turret 1 degree
                if (!(middle_x - red_x < 30))
                {
                    delta_x = 2;
                }
                else if (!(middle_x - red_x < 5))
                {
                    delta_x = 1;
                }
            }
            else if (red_x > middle_x)
            {
                if (!(red_x - middle_x < 30))
                {
                    delta_x = -2;
                }
                else if (!(red_x - middle_x < 5))
                {
                    delta_x = -1;
                }
            }

            // the same process is done for y but the signs are switched (ngl it makes my head hurt when I try to think about why it works like that but it just does)
            if (red_y < middle_y)
            {
                if (!(middle_y - red_y < 30))
                {
                    delta_y = -2;
                }
                else if (!(middle_y - red_y < 5))
                {
                    delta_y = -1;
                }
            }
            else if (red_y > middle_y)
            {
                if (!(red_y - middle_y < 30))
                {
                    delta_y = 2;
                }
                else if (!(red_y - middle_y < 5))
                {
                    delta_y = 1;
                }
            }

//            writeToPort(delta_x, delta_y, fire);


            pic.Image = img;
        }

        // writeToPort sends a string to the arduino that can be spliced for information
        public void writeToPort(int dx, int dy, bool fire)
        {
            // this whole if statement is used to determine whether or not the turret will fire

            // if either the x value or the y value are not equal to 0 (aka, if the turret is not aimed at the target), then the turret will not fire and the shoot_watch will be reset
            if (dx != 0 || dy != 0)
            {
                shoot_watch = Stopwatch.StartNew();
                // this if statement makes it so that the program only sends instructions to the arduino every 500 milliseconds
                // this is extremely important because if the program sends instructions faster than the camera refreshes, then it will overshoot the target and then overcorrect and it will just go back and forth instead of aiming directly at the target
                if (watch.ElapsedMilliseconds > 1000)
                {
                    watch = Stopwatch.StartNew();
                    port.Write(string.Format("X{0}Y{1}C{2}", dx, dy, 0));
                    Console.WriteLine(string.Format("X{0}Y{1}C{2}", dx, dy, 0));
                }
            }
            else if (shoot_watch.ElapsedMilliseconds > 3000 && fire)
            {
                // if the turret is currently aiming at the target and has been aiming at it for at least three seconds, then it will fire
                shoot_watch = Stopwatch.StartNew();
                port.Write(string.Format("X{0}Y{1}C{2}", dx, dy, 1));
                Console.WriteLine(string.Format("X{0}Y{1}C{2}", dx, dy, 1));
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo filterInfo in filterInfoCollection)
                cboCamera.Items.Add(filterInfo.Name);
            cboCamera.SelectedIndex = 0;
            videoCaptureDevice = new VideoCaptureDevice();
            watch = Stopwatch.StartNew();
            shoot_watch = Stopwatch.StartNew();
//            port.Open();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (videoCaptureDevice.IsRunning == true)
                videoCaptureDevice.Stop();
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            port.Write(string.Format("X{0}Y{1}C{2}", 0, 0, 1));
            Console.WriteLine(string.Format("X{0}Y{1}C{2}", 0, 0, 1));
        }
    }
}