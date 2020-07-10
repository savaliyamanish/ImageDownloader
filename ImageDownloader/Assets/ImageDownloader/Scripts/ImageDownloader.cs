using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
public class ImageDownloader : MonoBehaviour
{
    private bool isLog = false;
    private float fadeTime = 1;
    private bool cached = true;

    private enum ImageType
    {
        none,
        uiImage,
        uiRawImage,
        renderer
    }

    private ImageType imageType = ImageType.none;
    private GameObject targetObj;
    private string url = null;

    private Texture2D loadingImage, errorImage;

    private UnityAction onStartAction,
        onDownloadedAction,
        OnLoadedAction,
        onEndAction;

    private UnityAction<int> onDownloadProgressChange;
    private UnityAction<string> onErrorAction;

    private static Dictionary<string, ImageDownloader> underProcessDownloader
        = new Dictionary<string, ImageDownloader>();

    private string uniqueHash;
    private int progress;

    private bool success = false;

    static string filePath = Application.persistentDataPath + "/" +
             "ImageCatch" + "/";


    /// <summary>
    /// Get instance of ImageDownloader class
    /// </summary>
    public static ImageDownloader get()
    {
        return new GameObject("ImageDownloader").AddComponent<ImageDownloader>();
    }

    /// <summary>
    /// Set image url for download.
    /// </summary>
    /// <param name="url">Image Url</param>
    /// <returns></returns>
    public ImageDownloader load(string url)
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Url set : " + url);

        this.url = url;
        return this;
    }

    /// <summary>
    /// Set fading animation time.
    /// </summary>
    /// <param name="fadeTime">Fade animation time. Set 0 for disable fading.</param>
    /// <returns></returns>
    public ImageDownloader setFadeTime(float fadeTime)
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Fading time set : " + fadeTime);

        this.fadeTime = fadeTime;
        return this;
    }

    /// <summary>
    /// Set target Image component.
    /// </summary>
    /// <param name="image">target Unity UI image component</param>
    /// <returns></returns>
    public ImageDownloader into(Image image)
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Target as UIImage set : " + image);

        imageType = ImageType.uiImage;
        this.targetObj = image.gameObject;
        return this;
    }
    /// <summary>
    /// Set target Image component.
    /// </summary>
    /// <param name="image">target Unity UI RowImage component</param>
    /// <returns></returns>
    public ImageDownloader into(RawImage image)
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Target as UIRow set : " + image);

        imageType = ImageType.uiRawImage;
        this.targetObj = image.gameObject;
        return this;
    }

    /// <summary>
    /// Set target Renderer component.
    /// </summary>
    /// <param name="renderer">target renderer component</param>
    /// <returns></returns>
    public ImageDownloader into(Renderer renderer)
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Target as Renderer set : " + renderer);

        imageType = ImageType.renderer;
        this.targetObj = renderer.gameObject;
        return this;
    }

    #region Actions
    public ImageDownloader withStartAction(UnityAction action)
    {
        this.onStartAction = action;

        if (isLog)
            Debug.Log("[ImageDownloader] On start action set : " + action);

        return this;
    }

    public ImageDownloader withDownloadedAction(UnityAction action)
    {
        this.onDownloadedAction = action;

        if (isLog)
            Debug.Log("[ImageDownloader] On downloaded action set : " + action);

        return this;
    }

    public ImageDownloader withDownloadProgressChangedAction(UnityAction<int> action)
    {
        this.onDownloadProgressChange = action;

        if (isLog)
            Debug.Log("[ImageDownloader] On download progress changed action set : " + action);

        return this;
    }

    public ImageDownloader withLoadedAction(UnityAction action)
    {
        this.OnLoadedAction = action;

        if (isLog)
            Debug.Log("[ImageDownloader] On loaded action set : " + action);

        return this;
    }

    public ImageDownloader withErrorAction(UnityAction<string> action)
    {
        this.onErrorAction = action;

        if (isLog)
            Debug.Log("[ImageDownloader] On error action set : " + action);

        return this;
    }

    public ImageDownloader withEndAction(UnityAction action)
    {
        this.onEndAction = action;

        if (isLog)
            Debug.Log("[ImageDownloader] On end action set : " + action);

        return this;
    }
    #endregion

    /// <summary>
    /// Show or hide logs in console.
    /// </summary>
    /// <param name="enable">'true' for show logs in console.</param>
    /// <returns></returns>
    public ImageDownloader setEnableLog(bool enableLog)
    {
        this.isLog = enableLog;

        if (enableLog)
            Debug.Log("[ImageDownloader] Logging enabled : " + enableLog);

        return this;
    }

    /// <summary>
    /// Set the sprite of image when ImageDownloader is downloading and loading image
    /// </summary>
    /// <param name="loadingPlaceholder">loading texture</param>
    /// <returns></returns>
    public ImageDownloader setLoadingPlaceholder(Texture2D loadingPlaceholder)
    {
        this.loadingImage = loadingPlaceholder;

        if (isLog)
            Debug.Log("[ImageDownloader] Loading placeholder has been set.");

        return this;
    }

    /// <summary>
    /// Set image sprite when some error occurred during downloading or loading image
    /// </summary>
    /// <param name="errorPlaceholder">error texture</param>
    /// <returns></returns>
    public ImageDownloader setErrorPlaceholder(Texture2D errorPlaceholder)
    {
        this.errorImage = errorPlaceholder;

        if (isLog)
            Debug.Log("[ImageDownloader] Error placeholder has been set.");

        return this;
    }

    /// <summary>
    /// Enable cache
    /// </summary>
    /// <returns></returns>
    public ImageDownloader setCached(bool cached)
    {
        this.cached = cached;

        if (isLog)
            Debug.Log("[ImageDownloader] Cache enabled : " + cached);

        return this;
    }

    /// <summary>
    /// Start ImageDownloader process.
    /// </summary>
    public void start()
    {
        if (url == null)
        {
            error("Url has not been set. Use 'load' funtion to set image url.");
            return;
        }

        try
        {
            Uri uri = new Uri(url);
            this.url = uri.AbsoluteUri;
        }
        catch (Exception ex)
        {
            error("Url is not correct.");
            return;
        }

        if (imageType == ImageType.none || targetObj == null)
        {
            error("Target has not been set. Use 'into' function to set target component.");
            // return;
        }

        if (isLog)
            Debug.Log("[ImageDownloader] Start Working.");

        if (loadingImage != null)
            SetLoadingImage();

        if (onStartAction != null)
            onStartAction.Invoke();

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        uniqueHash = CreateMD5(url);

        if (underProcessDownloader.ContainsKey(uniqueHash))
        {
            ImageDownloader sameProcess = underProcessDownloader[uniqueHash];
            sameProcess.onDownloadedAction += () =>
            {
                if (onDownloadedAction != null)
                    onDownloadedAction.Invoke();

                loadSpriteToImage();
            };
        }
        else
        {
            if (File.Exists(filePath + uniqueHash))
            {
                if (onDownloadedAction != null)
                    onDownloadedAction.Invoke();

                loadSpriteToImage();
            }
            else
            {
                underProcessDownloader.Add(uniqueHash, this);
                StopAllCoroutines();
                StartCoroutine("Downloader");
            }
        }
    }

    private IEnumerator Downloader()
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Download started.");

        var www = new WWW(url);

        while (!www.isDone)
        {
            if (www.error != null)
            {
                error("Error while downloading the image : " + www.error);
                yield break;
            }

            progress = Mathf.FloorToInt(www.progress * 100);
            if (onDownloadProgressChange != null)
                onDownloadProgressChange.Invoke(progress);

            if (isLog)
                Debug.Log("[ImageDownloader] Downloading progress : " + progress + "%");

            yield return null;
        }

        if (www.error == null)
            File.WriteAllBytes(filePath + uniqueHash, www.bytes);

        www.Dispose();
        www = null;

        if (onDownloadedAction != null)
            onDownloadedAction.Invoke();

        loadSpriteToImage();

        underProcessDownloader.Remove(uniqueHash);
    }

    private void loadSpriteToImage()
    {
        progress = 100;
        if (onDownloadProgressChange != null)
            onDownloadProgressChange.Invoke(progress);

        if (isLog)
            Debug.Log("[ImageDownloader] Downloading progress : " + progress + "%");

        if (!File.Exists(filePath + uniqueHash))
        {
            error("Loading image file has been failed.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(ImageLoader());
    }

    private void SetLoadingImage()
    {
         if (imageType == ImageType.none || targetObj == null)
        {
            error("Target has not been set.");
            return;
        }

        switch (imageType)
        {
            case ImageType.renderer:
                Renderer renderer = targetObj.GetComponent<Renderer>();
                renderer.material.mainTexture = loadingImage;
                break;

            case ImageType.uiImage:
                Image image = targetObj.GetComponent<Image>();
                Sprite sprite = Sprite.Create(loadingImage,
                     new Rect(0, 0, loadingImage.width, loadingImage.height),
                     new Vector2(0.5f, 0.5f));
                image.sprite = sprite;
                break;
            case ImageType.uiRawImage:
                RawImage rawImage = targetObj.GetComponent<RawImage>();
                rawImage.texture=loadingImage;

                break;
        }

    }

    private IEnumerator ImageLoader(Texture2D texture = null)
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Start loading image.");

        if (texture == null)
        {
            byte[] fileData;
            fileData = File.ReadAllBytes(filePath + uniqueHash);
            texture = new Texture2D(2, 2);
            //ImageConversion.LoadImage(texture, fileData);
            texture.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        }

        Color color;
         if (imageType != ImageType.none && targetObj != null)
        {
            switch (imageType)
            {
                case ImageType.renderer:
                    Renderer renderer = targetObj.GetComponent<Renderer>();
                    renderer.material.mainTexture = texture;
                    color = renderer.material.color;
                    float maxAlpha = color.a;

                    if (fadeTime > 0)
                    {
                        color.a = 0;
                        renderer.material.color = color;

                        float time = Time.time;
                        while (color.a < maxAlpha)
                        {
                            color.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);
                            renderer.material.color = color;
                            yield return null;
                        }
                    }

                    break;

                case ImageType.uiImage:
                    Image image = targetObj.GetComponent<Image>();
                    Sprite sprite = Sprite.Create(texture,
                        new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    image.sprite = sprite;
                    color = image.color;
                    maxAlpha = color.a;

                    if (fadeTime > 0)
                    {
                        color.a = 0;
                        image.color = color;

                        float time = Time.time;
                        while (color.a < maxAlpha)
                        {
                            color.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);
                            image.color = color;
                            yield return null;
                        }
                    }
                    break;
                case ImageType.uiRawImage:
                    RawImage rawImage = targetObj.GetComponent<RawImage>();
                    rawImage.texture = texture;
                    color = rawImage.color;
                    maxAlpha = color.a;

                    if (fadeTime > 0)
                    {
                        color.a = 0;
                        rawImage.color = color;

                        float time = Time.time;
                        while (color.a < maxAlpha)
                        {
                            color.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);
                            rawImage.color = color;
                            yield return null;
                        }
                    }

                    break;
            }
        }
        if (OnLoadedAction != null)
            OnLoadedAction.Invoke();

        if (isLog)
            Debug.Log("[ImageDownloader] Image has been loaded.");

        success = true;
        finish();
    }

    public static string CreateMD5(string input)
    {
        // Use input string to calculate MD5 hash
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    private void error(string message)
    {
        success = false;

        if (isLog)
            Debug.LogError("[ImageDownloader] Error : " + message);

        if (onErrorAction != null)
            onErrorAction.Invoke(message);

        if (errorImage != null)
            StartCoroutine(ImageLoader(errorImage));
        else finish();
    }

    private void finish()
    {
        if (isLog)
            Debug.Log("[ImageDownloader] Operation has been finished.");

        if (!cached)
            File.Delete(filePath + uniqueHash);

        if (onEndAction != null)
            onEndAction.Invoke();

        Invoke("destroyer", 0.5f);
    }

    private void destroyer()
    {
        Destroy(gameObject);
    }
}
