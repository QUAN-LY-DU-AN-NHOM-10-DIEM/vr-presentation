using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;

namespace Assets.CustomPdfViewer.Scripts
{
    public class CustomPdfViewerUI : MonoBehaviour
    {
        [Header("UI References")]
        public RawImage pdfImage;      // display PDF page
        public TMP_Text pageIndicator;     // show current page
        public Button nextButton;      // next page button
        public Button previousButton;  // previous page button

        [Range(72, 300)] public int renderDPI = 150; // PDF render DPI

        [HideInInspector]
        public CustomPdfNavigator navigator;

        private string pdfPath;         // full path to PDF

        [Header("Room State")]
        public bool isInMainRoom = false;

        protected void Start()
        {
            nextButton?.onClick.AddListener(NextPage);
            previousButton?.onClick.AddListener(PreviousPage);
        }

        public void LoadPDF(string pdfFileName, bool isAbsolutePath = false)
        {
            if (navigator != null)
            {
                navigator.Dispose();
                navigator = null;
            }
            pdfPath = pdfFileName;

            // If it's NOT an absolute path, fall back to the default StreamingAssets behavior
            if (!isAbsolutePath)
            {
                pdfPath = Path.Combine(Application.streamingAssetsPath, pdfFileName);
            }
            //pdfPath = Path.Combine(Application.streamingAssetsPath, pdfFileName);

            // load PDF pages
            Texture2D[] pages = CustomPdfLoader.LoadPdfAsTextures(pdfPath, renderDPI);
            navigator = new CustomPdfNavigator(pages);

            UpdateUI(); // display first page
        }

        public void NextPage()
        {
            if (!isInMainRoom) return;

            navigator.Next();
            UpdateUI();
        }

        public void PreviousPage()
        {
            if (!isInMainRoom) return;

            navigator.Previous();
            UpdateUI();
        }

        public void GoToPage(int pageNumber)
        {
            if (!isInMainRoom) return;

            navigator.GoTo(pageNumber);
            UpdateUI();
        }

        // update RawImage, page text, button states
        private void UpdateUI()
        {
            if (navigator.Pages.Length == 0 || pdfImage == null) return;

            pdfImage.texture = navigator.Pages[navigator.CurrentPage];

            if (pageIndicator != null)
                pageIndicator.text = $"Page {navigator.CurrentPage + 1} / {navigator.TotalPages}";

            if (nextButton != null)
                nextButton.interactable = navigator.CurrentPage < navigator.TotalPages - 1;

            if (previousButton != null)
                previousButton.interactable = navigator.CurrentPage > 0;
        }

        public void EnablePresentationMode()
        {
            isInMainRoom = true;
        }

        private void OnDestroy()
        {
            // free textures when object is destroyed
            if (navigator?.Pages != null)
            {
                foreach (var page in navigator.Pages)
                {
                    if (page != null) Destroy(page);
                }
            }
        }
    }
}