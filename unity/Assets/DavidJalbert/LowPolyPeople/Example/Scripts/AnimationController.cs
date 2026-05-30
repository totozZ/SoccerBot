using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DavidJalbert.LowPolyPeople
{
    public class AnimationController : MonoBehaviour
    {
        public Animator[] characters;
        public Text label;
        public Material[] palettes;
        public Camera[] cameras;

        private int currentCamera = 0;

        void Start()
        {
            setAnimation("idle");
            setCamera(0);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) setAnimation("idle");
            if (Input.GetKeyDown(KeyCode.Alpha2)) setAnimation("walk");
            if (Input.GetKeyDown(KeyCode.Alpha3)) setAnimation("run");
            if (Input.GetKeyDown(KeyCode.Alpha4)) setAnimation("wave");
            if (Input.GetKeyDown(KeyCode.R)) randomizePalette();
            if (Input.GetKeyDown(KeyCode.C)) changeCamera();
        }

        public void setAnimation(string tag)
        {
            label.text = "Current animation: " + tag;
            foreach (Animator animator in characters) animator.SetTrigger(tag);
        }

        public void randomizePalette()
        {
            foreach (Animator animator in characters)
            {
                SkinnedMeshRenderer renderer = animator.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = palettes[(int)(Random.value * palettes.Length) % palettes.Length];
                }
            }
        }

        public void setCamera(int c)
        {
            currentCamera = c % cameras.Length;
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].gameObject.SetActive(currentCamera == i);
            }
        }

        public void changeCamera()
        {
            setCamera(currentCamera + 1);
        }
    }
}