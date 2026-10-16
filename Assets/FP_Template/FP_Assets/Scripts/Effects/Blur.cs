using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blur : MonoBehaviour
{
	private static Blur _instance;
	public static Blur Instance { get { return _instance; } }

	private Canvas canvas;
	private GameObject blurImage;

	private void Awake()
	{
		if (_instance != null && _instance != this)
		{
			Destroy(gameObject);
			return;
		}
		_instance = this;
	}

	private void Start() {
		canvas = GetComponent<Canvas>();
		if (canvas.GetComponent<Camera>() == null){
			canvas.worldCamera = Camera.main;
		}
		blurImage = transform.GetChild(0).gameObject;
	}
	
	public void SetBlur(bool value){
		blurImage.SetActive(value);
	}
}
