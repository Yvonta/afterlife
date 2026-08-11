using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Yvonta.UI
{
    public class UIRegister : MonoBehaviour
    {
        private TMP_InputField emailInputField;
        private TMP_InputField passwordInputField;
        private TMP_InputField nameInputField;
        private TMP_Dropdown genderDropdown;
        private TMP_InputField ageInputField;
        private Button registerButton;
        private Button backButton;
        private TextMeshProUGUI statusText;
        private GameObject registerPanelObj;

        public event Action<string, string, string, string, string> OnRegisterSubmitted;
        public event Action OnBackClicked;

        public void BuildUI(Transform parentCanvasTransform)
        {
            registerPanelObj = new GameObject("RegisterPanel");
            registerPanelObj.transform.SetParent(parentCanvasTransform, false);
            RectTransform panelRect = registerPanelObj.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 600);
            Image panelImage = registerPanelObj.AddComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            VerticalLayoutGroup layout = registerPanelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 20, 20);
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_InputField CreateInputField(string placeholderText, TMP_InputField.ContentType contentType)
            {
                GameObject inputObj = new GameObject("InputField_" + placeholderText);
                inputObj.transform.SetParent(registerPanelObj.transform, false);
                inputObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
                
                Image bg = inputObj.AddComponent<Image>();
                bg.color = new Color(0.9f, 0.9f, 0.9f);

                TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
                inputField.contentType = contentType;

                GameObject textArea = new GameObject("TextArea");
                textArea.transform.SetParent(inputObj.transform, false);
                RectTransform taRect = textArea.AddComponent<RectTransform>();
                taRect.anchorMin = Vector2.zero;
                taRect.anchorMax = Vector2.one;
                taRect.offsetMin = new Vector2(10, 5);
                taRect.offsetMax = new Vector2(-10, -5);

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(textArea.transform, false);
                TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
                textComp.fontSize = 15;
                textComp.color = Color.black;

                GameObject holderObj = new GameObject("Placeholder");
                holderObj.transform.SetParent(textArea.transform, false);
                TextMeshProUGUI holderComp = holderObj.AddComponent<TextMeshProUGUI>();
                holderComp.text = placeholderText;
                holderComp.fontSize = 15;
                holderComp.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);

                inputField.textComponent = textComp;
                inputField.placeholder = holderComp;
                return inputField;
            }

            emailInputField = CreateInputField("Enter email...", TMP_InputField.ContentType.EmailAddress);
            passwordInputField = CreateInputField("Enter password...", TMP_InputField.ContentType.Password);
            nameInputField = CreateInputField("Enter name...", TMP_InputField.ContentType.Standard);

            // Gender Dropdown Implementation
            GameObject dropdownObj = new GameObject("Dropdown_Gender");
            dropdownObj.transform.SetParent(registerPanelObj.transform, false);
            dropdownObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 40);

            Image dropdownBg = dropdownObj.AddComponent<Image>();
            dropdownBg.color = new Color(0.9f, 0.9f, 0.9f);

            genderDropdown = dropdownObj.AddComponent<TMP_Dropdown>();

            // Setup Dropdown Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-25, 0);

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 15;
            labelText.color = Color.black;
            genderDropdown.captionText = labelText;

            // Setup Dropdown Template
            GameObject templateObj = new GameObject("Template");
            templateObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform templateRect = templateObj.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0, 2);
            templateRect.sizeDelta = new Vector2(0, 70);

            Image templateImage = templateObj.AddComponent<Image>();
            templateImage.color = new Color(0.95f, 0.95f, 0.95f);

            ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            Mask viewportMask = viewportObj.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            Image viewportImage = viewportObj.AddComponent<Image>();

            scrollRect.viewport = viewportRect;

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0, 60);

            scrollRect.content = contentRect;

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 30);

            Toggle itemToggle = itemObj.AddComponent<Toggle>();

            GameObject itemBackgroundObj = new GameObject("Item Background");
            itemBackgroundObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemBgRect = itemBackgroundObj.AddComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            Image itemBgImage = itemBackgroundObj.AddComponent<Image>();
            itemBgImage.color = new Color(0.8f, 0.8f, 0.8f);
            itemToggle.targetGraphic = itemBgImage;

            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);

            TextMeshProUGUI itemLabelText = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabelText.fontSize = 15;
            itemLabelText.color = Color.black;

            itemToggle.graphic = itemBgImage;
            genderDropdown.itemText = itemLabelText;
            genderDropdown.template = templateRect;

            // Populate options
            genderDropdown.options.Clear();
            genderDropdown.options.Add(new TMP_Dropdown.OptionData("Male"));
            genderDropdown.options.Add(new TMP_Dropdown.OptionData("Female"));
            genderDropdown.RefreshShownValue();
            templateObj.SetActive(false);

            ageInputField = CreateInputField("Enter age...", TMP_InputField.ContentType.IntegerNumber);

            Button CreateButton(string labelTextStr, ColorBlock colors)
            {
                GameObject btnObj = new GameObject("Button_" + labelTextStr);
                btnObj.transform.SetParent(registerPanelObj.transform, false);
                btnObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 38);

                Image btnImg = btnObj.AddComponent<Image>();
                Button btn = btnObj.AddComponent<Button>();
                btn.colors = colors;

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform tRect = textObj.AddComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;

                TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
                btnText.text = labelTextStr;
                btnText.fontSize = 15;
                btnText.alignment = TextAlignmentOptions.Center;
                btnText.color = Color.white;

                return btn;
            }

            ColorBlock defaultColors = ColorBlock.defaultColorBlock;
            defaultColors.normalColor = new Color(0.2f, 0.6f, 0.9f);
            defaultColors.highlightedColor = new Color(0.3f, 0.7f, 1f);

            registerButton = CreateButton("Register", defaultColors);
            backButton = CreateButton("Back", defaultColors);

            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(registerPanelObj.transform, false);
            statusObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 25);
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 13;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.yellow;

            if (registerButton != null) registerButton.onClick.AddListener(HandleRegisterClicked);
            if (backButton != null) backButton.onClick.AddListener(HandleBackClicked);

            if (emailInputField != null)
            {
                emailInputField.Select();
                emailInputField.ActivateInputField();
            }

            SetVisible(false);
        }

        public void SetVisible(bool isVisible)
        {
            if (registerPanelObj != null)
            {
                registerPanelObj.SetActive(isVisible);
            }
        }

        private void HandleRegisterClicked()
        {
            string email = emailInputField != null ? emailInputField.text.Trim() : string.Empty;
            string password = passwordInputField != null ? passwordInputField.text : string.Empty;
            string name = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
            string gender = genderDropdown != null ? genderDropdown.options[genderDropdown.value].text : string.Empty;
            string age = ageInputField != null ? ageInputField.text.Trim() : string.Empty;

            if (ValidateInputs(email, password, name, gender, age))
            {
                SetStatusMessage(string.Empty);
                OnRegisterSubmitted?.Invoke(email, password, name, gender, age);
            }
        }

        private void HandleBackClicked()
        {
            OnBackClicked?.Invoke();
        }

        private bool ValidateInputs(string email, string password, string name, string gender, string age)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@") || !email.Contains("."))
            {
                SetStatusMessage("Please enter a valid email address.");
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatusMessage("Please enter your password.");
                return false;
            }

            if (string.IsNullOrEmpty(name))
            {
                SetStatusMessage("Please enter your name.");
                return false;
            }

            if (string.IsNullOrEmpty(gender))
            {
                SetStatusMessage("Please select your gender.");
                return false;
            }

            if (string.IsNullOrEmpty(age))
            {
                SetStatusMessage("Please enter your age.");
                return false;
            }

            return true;
        }

        public void SetStatusMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void SetInteractable(bool state)
        {
            if (emailInputField != null) emailInputField.interactable = state;
            if (passwordInputField != null) passwordInputField.interactable = state;
            if (nameInputField != null) nameInputField.interactable = state;
            if (genderDropdown != null) genderDropdown.interactable = state;
            if (ageInputField != null) ageInputField.interactable = state;
            if (registerButton != null) registerButton.interactable = state;
            if (backButton != null) backButton.interactable = state;
        }
    }
}