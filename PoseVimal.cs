using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity.CoordinateSystem;

using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mediapipe.Unity
{
  public class PoseVimal : MonoBehaviour
  {
    [SerializeField] private TextAsset _configAsset;
    [SerializeField] private RawImage _screen;
    [SerializeField] private int _width;
    [SerializeField] private int _height;
    [SerializeField] private int _fps;
    [SerializeField] private PoseLandmarkListAnnotationController _PoseLandmarksAnnotationController;

    private CalculatorGraph _graph;
    private ResourceManager _resourceManager;

    private WebCamTexture _webCamTexture;
    private Texture2D _inputTexture;
    private Color32[] _inputPixelData;
    private Texture2D _outputTexture;
    private Color32[] _outputPixelData;
    private int camsel = 1;

    public GameObject noseSphere;

    private IEnumerator Start()
    {
      Glog.Initialize("MediaPipeUnityPlugin");
      Glog.Logtostderr = true; // when true, log will be output to `Editor.log` / `Player.log` 
      Glog.Minloglevel = 0; // output INFO logs
      Glog.V = 3; // output more verbose logs

      if (WebCamTexture.devices.Length == 0)
      {
        throw new System.Exception("Web Camera devices are not found");
      }
      var webCamDevice = WebCamTexture.devices[camsel];
      _webCamTexture = new WebCamTexture(webCamDevice.name, _width, _height, _fps);
      _webCamTexture.Play();

      yield return new WaitUntil(() => _webCamTexture.width > 16);

      _screen.rectTransform.sizeDelta = new Vector2(_width, _height);

      _inputTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
      _inputPixelData = new Color32[_width * _height];
      _outputTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
      _outputPixelData = new Color32[_width * _height];

      // _screen.texture = _outputTexture;
      _screen.texture = _webCamTexture;

      AssetLoader.Provide(new StreamingAssetsResourceManager());
      yield return AssetLoader.PrepareAssetAsync("pose_landmark_full.bytes", "pose_landmark_full.bytes", false); 
      yield return AssetLoader.PrepareAssetAsync("pose_detection.bytes", "pose_detection.bytes", false);
 
      var stopwatch = new Stopwatch();

            //_graph = new CalculatorGraph(_configAsset.text);
            var config = CalculatorGraphConfig.Parser.ParseFromTextFormat(_configAsset.text);
            using (var validatedGraphConfig = new ValidatedGraphConfig())
            {
                validatedGraphConfig.Initialize(config).AssertOk();
                _graph = new CalculatorGraph(validatedGraphConfig.Config());
            }

            //var outputVideoStream = new OutputStream<ImageFramePacket, ImageFrame>(_graph, "segmentation_mask");
            var outputVideoStream = new OutputStream<ImageFramePacket, ImageFrame>(_graph, "output_video");
	  var poseLandmarkStream = new OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList>(_graph, "pose_landmarks");
      outputVideoStream.StartPolling().AssertOk();
      poseLandmarkStream.StartPolling().AssertOk();
      stopwatch.Start();

      _graph.StartRun().AssertOk();

      var screenRect = _screen.GetComponent<RectTransform>().rect;
             
      while (true)
      {
        _inputTexture.SetPixels32(_webCamTexture.GetPixels32(_inputPixelData));
        var imageFrame = new ImageFrame(ImageFormat.Types.Format.Srgba, _width, _height, _width * 4, _inputTexture.GetRawTextureData<byte>());
        var currentTimestamp = stopwatch.ElapsedTicks / (System.TimeSpan.TicksPerMillisecond / 1000);
        _graph.AddPacketToInputStream("input_video", new ImageFramePacket(imageFrame, new Timestamp(currentTimestamp))).AssertOk();

        yield return new WaitForEndOfFrame();

        /*
         if (outputVideoStream.TryGetNext(out var outputVideo))
         {
           if (outputVideo.TryReadPixelData(_outputPixelData))
           {
             _outputTexture.SetPixels32(_outputPixelData);
             _outputTexture.Apply();
           }
         }
         */
        if (poseLandmarkStream.TryGetNext(out var poseLandmarks))
        {
            
          if (poseLandmarks != null)
          {
            //_PoseLandmarksAnnotationController.rotationAngle = RotationAngle.Rotation180;
            _PoseLandmarksAnnotationController.isMirrored = true;
            _PoseLandmarksAnnotationController.DrawNow(poseLandmarks);
            // punho esquerdo
            //var nose = poseLandmarks.Landmark[16];
            //var screenPoint = screenRect.GetPoint(nose);
            //var scale = 0.1545784f; // TODO: Not sure how to get this number programmatically
            //noseSphere.transform.position = new Vector3(-screenPoint.x * scale, screenPoint.y * scale, 90f);
            //Debug.Log($"Unity Local Coordinates: {screenRect.GetPoint(nose)}, Image Coordinates: {nose}");
          }
        }
        else
        {
             //_PoseLandmarksAnnotationController.DrawNow(null);
        }
      }
    }

        private void Update()
        {
            if (Input.GetTouch(0).tapCount == 2)
            {
                //Double tap
               
            }

            if (Input.GetTouch(0).tapCount == 1)
            {
                //Single tap
            }
        }

        private void OnDestroy()
    {
      if (_webCamTexture != null)
      {
        _webCamTexture.Stop();
      }

      if (_graph != null)
      {
        try
        {
          _graph.CloseInputStream("input_video").AssertOk();
          _graph.WaitUntilDone().AssertOk();
        }
        finally
        {

          _graph.Dispose();
        }
      }
    }
  }
}