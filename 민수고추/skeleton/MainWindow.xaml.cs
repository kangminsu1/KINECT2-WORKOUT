using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace skeleton
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor _sensor;
        private readonly Brush[] _SkeletonBrushes;
        private Skeleton[] _FrameSkeletons;
        private JointType[] joints;
        private WriteableBitmap _ColorImageBitmap;
        private Int32Rect _ColorImageBitmapRect;
        private int _ColorImageStride;
        private const float WAVE_THRESHOLD = 0.1f;
        private const int WAVE_MOVEMENT_TIMEOUT = 5000;
        private const int REQUIRED_ITERATIONS = 1;
        private WaveGestureTracker[,] _PlayerWaveTracker = new WaveGestureTracker[6, 2];
        private enum WavePosition
        {
            None = 0,
            UP = 1,
           DOWN = 2,
           NETRUAL = 3
        }
        private enum WaveGestureState
        {
            None = 0,
            Success = 1,
            Failure = 2,
            Inprogress = 3
        }
        
        private struct WaveGestureTracker
        {
            public int IterationCount;
            public WaveGestureState State;
            public WavePosition StartPosition;
            public WavePosition CurrentPosition;
            public long Timestamp;
            public void Reset()
            {
                IterationCount = 0;
                State = WaveGestureState.None;
                Timestamp = 0;
                StartPosition = WavePosition.None;
                CurrentPosition = WavePosition.None;
            }

            internal void UpdatePosition(WavePosition position, long timestamp)
            {
                if (CurrentPosition != position)
                {
                    if (position == WavePosition.DOWN || position == WavePosition.UP)
                    {
                        IterationCount++;
                        if (IterationCount == 2)
                        {
                            State = WaveGestureState.Inprogress;
                            IterationCount = 0;
                            StartPosition = position;
                        }

                    }
                    CurrentPosition = position;
                    Timestamp = timestamp;
                }
            }

            internal void UpdateState(WaveGestureState state, long timestamp)
            {
                State = state;
                Timestamp = timestamp;
            }
        }
        //-----------------------키넥트 인식받기----------------------------------------------
        public MainWindow()
        {
            InitializeComponent();
             _SkeletonBrushes = new[] { Brushes.Black, Brushes.Crimson, Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink };
            KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChange;
             KinectDevice = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
            errorBox.Opacity = 0;
        }
        private void KinectSensors_StatusChange(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Initializing:
                case KinectStatus.Connected:
                     KinectDevice = e.Sensor;
                    break;
                case KinectStatus.Disconnected:
                     KinectDevice = null;
                    break;
                default:
                    break;
            }
        }
        public KinectSensor KinectDevice
        {
            get
            {
                return  _sensor;
            }
            set
            {
                if ( _sensor != value)
                {
                    if ( _sensor != null)
                    {
                        Kinectdie();
                    }
                     _sensor = value;

                    if ( _sensor != null)
                    {
                        if ( _sensor.Status == KinectStatus.Connected)
                        {
                            kinectopen();
                            
                        }
                    }
                }
            }
        }
        private void Kinectdie()
        {
            _sensor.Stop();
            _sensor.SkeletonFrameReady -= Kinect_SkeletonFreamReady;
            _sensor.ColorFrameReady -= Kinect_ColorFreamReady;
            _sensor.ColorStream.Disable();
            _sensor.DepthStream.Disable();
            _sensor.SkeletonStream.Disable();
            _FrameSkeletons = null;
        }
        private void kinectopen()
        {
            _sensor.SkeletonStream.Enable();
            _sensor.DepthStream.Enable();
            _sensor.ColorStream.Enable();
            _FrameSkeletons = new Skeleton[_sensor.SkeletonStream.FrameSkeletonArrayLength];
            _sensor.SkeletonFrameReady += Kinect_SkeletonFreamReady;
            _sensor.ColorFrameReady += Kinect_ColorFreamReady;
            ColorImageBitmap(_sensor);
            _sensor.Start();
        }
        //-------------------------컬러맵 생성-------------------------------------------------
        private void ColorImageBitmap(KinectSensor sensor)            //프레임 이미지 효율적 생성
        {


            ColorImageStream colorStream = sensor.ColorStream;
            _ColorImageBitmap = new WriteableBitmap(colorStream.FrameWidth, colorStream.FrameHeight, 96, 96,
                                                         PixelFormats.Bgr32, null);                        // 높이, 넓이 조정
            _ColorImageBitmapRect = new Int32Rect(0, 0, colorStream.FrameWidth, colorStream.FrameHeight); //x, y 의 기준점
            _ColorImageStride = colorStream.FrameWidth * colorStream.FrameBytesPerPixel;                 //행의 넓이

            colorimage.Source = _ColorImageBitmap;
        }
        private void Kinect_ColorFreamReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame frame = e.OpenColorImageFrame())
            {
                if (frame != null)
                {
                    byte[] pixelData = new byte[frame.PixelDataLength];
                    frame.CopyPixelDataTo(pixelData);

                    _ColorImageBitmap.WritePixels(_ColorImageBitmapRect, pixelData, _ColorImageStride, 0);

                }
            }
        }

        //----------------------------------------스켈 초기화(막대모형 생성)

        private void Kinect_SkeletonFreamReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    Brush userBrush;
                    Skeleton skeleton;
                    Layout.Children.Clear();
                    frame.CopySkeletonDataTo( _FrameSkeletons);
                    int frameTimestamp = 10000;
                    for (int i = 0; i <  _FrameSkeletons.Length; i++)
                    {
                        skeleton =  _FrameSkeletons[i];
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                           
                            userBrush =  _SkeletonBrushes[i %  _SkeletonBrushes.Length];

                            joints = new[] {JointType.Head, JointType.ShoulderCenter, JointType.ShoulderLeft, JointType.Spine, JointType.ShoulderRight, JointType.ShoulderCenter, JointType.HipCenter, JointType.HipLeft
                                             , JointType.Spine, JointType.HipRight, JointType.HipCenter};
                            Layout.Children.Add(CreateFigure(skeleton, userBrush, joints));

                            joints = new[] { JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft };
                            Layout.Children.Add(CreateFigure(skeleton, userBrush, joints));

                            joints = new[] { JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight };
                            Layout.Children.Add(CreateFigure(skeleton, userBrush, joints));

                            joints = new[] { JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft };
                            Layout.Children.Add(CreateFigure(skeleton, userBrush, joints));

                            joints = new[] { JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight };
                            Layout.Children.Add(CreateFigure(skeleton, userBrush, joints));


                            if (Lateralbutten.IsChecked == true)
                            {
                               Lateral(skeleton, true, ref this._PlayerWaveTracker[i, 0], frameTimestamp);
                               Lateral(skeleton, false, ref this._PlayerWaveTracker[i, 1], frameTimestamp);
                                image11.Opacity = 100;
                                image22.Opacity = 0;
                                image33.Opacity = 0;
                            }
                            else if (SqurtButten.IsChecked == true)
                            {
                                Squrt(skeleton, true, ref _PlayerWaveTracker[i, 0],frameTimestamp);
                                Squrt(skeleton, false, ref _PlayerWaveTracker[i, 1],frameTimestamp);
                                image11.Opacity = 0;
                                image22.Opacity = 100;
                                image33.Opacity = 0;
                            }
                            else if (PushupButton.IsChecked == true)
                            {

                                shoulderpress(skeleton, true, ref _PlayerWaveTracker[i, 0], frameTimestamp);
                                shoulderpress(skeleton, false, ref _PlayerWaveTracker[i, 1], frameTimestamp);
                                image11.Opacity = 0;
                                image22.Opacity = 0;
                                image33.Opacity = 100;
                            }
                        }
                        else
                        {
                            _PlayerWaveTracker[i, 0].Reset();
                            _PlayerWaveTracker[i, 1].Reset();
                           
                        }
                    }
                }
            }
        }



        //------------------------------바꿔야 할 부분 ---------------------------------------------------------------------
       
        private void Lateral(Skeleton skeleton, bool isLeft, ref WaveGestureTracker _PlayerWaveTracker, int frameTimestamp)
        {
            JointType elbowJointId = (isLeft) ? JointType.ElbowLeft : JointType.ElbowRight;
            JointType SholderJointId = (isLeft) ? JointType.ShoulderLeft : JointType.ShoulderRight;
            Joint elbow = skeleton.Joints[elbowJointId];
            Joint sholder = skeleton.Joints[SholderJointId];
            int y, x;
            double valu=elbow.Position.Y - sholder.Position.Y;
            
            if (sholder.TrackingState != JointTrackingState.NotTracked && elbow.TrackingState != JointTrackingState.NotTracked)
            {
                if (sholder.Position.Y < elbow.Position.Y)
                {
                        y = Convert.ToInt32(textmonst.Text) + 1;
                        textmonst.Text = Convert.ToString(y);
                     
                    if (valu >= 0.06f && valu <= 0.07f)
                    {
                        //여기다가 오류떳을때 기능 추가 (팔을 더 올렸을 때 발생함)
                        errorBox.Opacity = 100;
                        errorBox.Text = "팔을 내려주세요!";
                      
                    }
                    if(valu<0.06f)
                    {
                        errorBox.Opacity = 0;
                    }
               
                    if (textmonst.Text == "20")
                    {
                      x =  Convert.ToInt32(sets.Text) + 1;
                        sets.Text = Convert.ToString(x);
                        textchange();
                    }
                }
                else if (sholder.Position.Y > elbow.Position.Y)
                {
                     y = Convert.ToInt32(textmonst.Text) - 1;
                    textmonst.Text = Convert.ToString(y);
                if(textmonst.Text == "-10")
                    {
                        textmonst.Text = "0";
                    }
                }
            }
            else
            {
                _PlayerWaveTracker.Reset();
            }
        }
        private void Squrt(Skeleton skeleton, bool isLeft, ref WaveGestureTracker _PlayerWaveTracker, int frameTimestamp)
        {

            JointType KneeJointId = (isLeft) ? JointType.KneeLeft : JointType.KneeRight;
            JointType spinejoint = (isLeft) ? JointType.Spine: JointType.Spine;
            
            Joint knee = skeleton.Joints[KneeJointId];
            Joint spine = skeleton.Joints[spinejoint];

            int y, x;
            double valu = knee.Position.X - spine.Position.X;
            if (knee.TrackingState != JointTrackingState.NotTracked &&  spine.TrackingState != JointTrackingState.NotTracked)
            {

                if (valu >= 0.24f && valu < 0.3f)
                {
                    y = Convert.ToInt32(textmonst.Text) +1;
                    textmonst.Text = Convert.ToString(y);
                    if (textmonst.Text == "2")
                    {
                        x = Convert.ToInt32(sets.Text) + 1;
                        sets.Text = Convert.ToString(x);
                        textchange();
                    }
                }
               else if(valu >= 0.1f && valu < 0.13f)
                {
                    errorBox.Opacity = 100;
                    errorBox.Text = "발을 넓혀주세요!";
                    y = Convert.ToInt32(textmonst.Text) - 1;
                    textmonst.Text = Convert.ToString(y);
                    if (textmonst.Text == "-10")
                    {
                        textmonst.Text = "0";
                    }
                }
                else if(valu >= 0.11f && valu < 0.23f)
                {
                    errorBox.Opacity = 0;
                    errorBox.Text = "";
                    y = Convert.ToInt32(textmonst.Text) - 1;
                    textmonst.Text = Convert.ToString(y);
                    if (textmonst.Text == "-10")
                    {
                        textmonst.Text = "0";
                    }
                }
               
            }
            else
            {
                _PlayerWaveTracker.Reset();
            }
        }
        private void shoulderpress(Skeleton skeleton, bool isLeft, ref WaveGestureTracker _PlayerWaveTracker, int frameTimestamp)
        {
            JointType elbowJointId = (isLeft) ? JointType.ElbowLeft : JointType.ElbowRight;
            JointType SholderJointId = (isLeft) ? JointType.ShoulderLeft : JointType.ShoulderRight;
            JointType handJointId = (isLeft) ? JointType.HandLeft: JointType.HandRight;

            Joint elbow = skeleton.Joints[elbowJointId];
            Joint sholder = skeleton.Joints[SholderJointId];
            Joint hand = skeleton.Joints[handJointId];

            int y, x;
            double valu = elbow.Position.Y - sholder.Position.Y;
            double valuerror = hand.Position.X - elbow.Position.X;
            if (sholder.TrackingState != JointTrackingState.NotTracked && elbow.TrackingState != JointTrackingState.NotTracked)
            {
              
                    if (valuerror<=0.06f && valu >=0.17f)
                    {
                        y = Convert.ToInt32(textmonst.Text) + 1;
                        textmonst.Text = Convert.ToString(y);
                        
                        if (textmonst.Text == "20")
                        {
                            x = Convert.ToInt32(sets.Text) + 1;
                            sets.Text = Convert.ToString(x);
                            textchange();
                        }
                    }
                    else if (valu >= 0.0001f && valu<0.16f)
                    {
                        y = Convert.ToInt32(textmonst.Text) - 1;
                        textmonst.Text = Convert.ToString(y);
                        errorBox.Opacity = 0;
                    if (textmonst.Text == "-10")
                        {
                            textmonst.Text = "0";
                        }
                    }
                    else if (valu >= 0.1f && sholder.Position.Y <= 0.14f)
                {
                    //여기다가 오류떳을때 기능 추가 (팔꿈치 더 아래로 내렸을때 발생)
                    errorBox.Opacity = 100;
                    errorBox.Text = "팔꿈치를 더 올려주세요!";
                }
            }
            else
            {
                _PlayerWaveTracker.Reset();
            }
        }
        //------------------------------바꿔야 할 부분 ---------------------------------------------------------------------








        //-------------키넥트 조인트 포인드 받는거랑 색깔 넣는거-------------------------------------
        private Polyline CreateFigure(Skeleton skeleton, Brush brush, JointType[] joints)
        {
            Polyline figure = new Polyline();
            figure.StrokeThickness = 0;
            figure.Stroke = brush;

            for (int i = 0; i < joints.Length; i++)
            {
                figure.Points.Add(GetJointPoint(skeleton.Joints[joints[i]]));
            }
            return figure;
        }
        private Point GetJointPoint(Joint joint)
        {
            DepthImagePoint point =  _sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position,  _sensor.DepthStream.Format);

            point.X *= (int) Layout.ActualWidth /  _sensor.DepthStream.FrameWidth;
            point.Y *= (int) Layout.ActualHeight /  _sensor.DepthStream.FrameHeight;

            return new Point(point.X, point.Y);
           
        }
        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            int x;
            x = Convert.ToInt32(test2.Text) - 1;
            SETT.Text = Convert.ToString(x);
            goimage.Opacity = 100;
            Setting.Foreground = Brushes.Gold;
            sets.Text = "0";
        }
        private void textchange()
        {
            int t= Convert.ToInt32(SETT.Text), y;
            if (t>=0)
            {
                if (sets.Text == test1.Text)
                {
                    y = t - 1;
                    SETT.Text = Convert.ToString(y);
                    sets.Text = "0";
                    MessageBox.Show("한 세트 끝!"); 
                }
            }
            else
            {
                MessageBox.Show("모든 세트 끝!");
                SETT.Text = test2.Text;
                goimage.Opacity = 0;
                Setting.Foreground = Brushes.Black;
            }


        }
        //--------------------------------키넥트 기기 업다운-----------------------------------------
        private void Window_Closed(object sender, EventArgs e)
        {
            _sensor.ElevationAngle = 0;
            Kinectdie();
        }
        private void up_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _sensor.ElevationAngle += 5;
                slider_comments.Text = _sensor.ElevationAngle.ToString();
            }
            catch
            {
                _sensor.ElevationAngle = 0;
                slider_comments.Text = _sensor.ElevationAngle.ToString();
            }
        }
        private void down_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _sensor.ElevationAngle -= 5;
                slider_comments.Text = _sensor.ElevationAngle.ToString();
            }
            catch
            {
                _sensor.ElevationAngle = 0;
                slider_comments.Text = _sensor.ElevationAngle.ToString();
            }
        }
    }
}