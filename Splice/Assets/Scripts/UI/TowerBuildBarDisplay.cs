using Splice.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // World-space build-progress bar for a TowerCharacter — fills up while the tower is under construction,
    // then hides once it's built. Mirrors HealthBarDisplay; reads the networked BuildProgress01 only.
    public class TowerBuildBarDisplay : MonoBehaviour
    {
        [SerializeField] private TowerCharacter tower;
        [Tooltip("รากของแถบ (ปิดเมื่อสร้างเสร็จ) — ปล่อยว่างได้ถ้าใช้ทั้ง GameObject นี้")]
        [SerializeField] private GameObject bar;
        [SerializeField] private Image fillImage;
        [SerializeField] private Camera billboardCamera;

        private void Awake()
        {
            if (tower == null) tower = GetComponentInParent<TowerCharacter>();
            if (billboardCamera == null) billboardCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (tower == null) return;

            var constructing = tower.IsConstructing;
            if (bar != null) bar.SetActive(constructing);
            if (!constructing) return;

            if (fillImage != null) fillImage.fillAmount = tower.BuildProgress01;
            if (billboardCamera != null) transform.rotation = billboardCamera.transform.rotation;
        }
    }
}
