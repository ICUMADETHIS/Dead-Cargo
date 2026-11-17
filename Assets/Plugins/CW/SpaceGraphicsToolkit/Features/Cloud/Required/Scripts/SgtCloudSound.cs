using UnityEngine;
using System.Collections.Generic;
using CW.Common;

namespace SpaceGraphicsToolkit.Cloud
{
	/// <summary>This component manages environmental ambient audio loops (Rain and Thunderstorm) based on the camera's position relative to procedural cloud coverage and temperature layers.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Cloud Sound")]
	public class SgtCloudSound : MonoBehaviour
	{
		/// <summary>The audio clip used for environmental rain loop conditions.</summary>
		public AudioClip RainClip { set { rainClip = value; } get { return rainClip; } } [SerializeField] private AudioClip rainClip;

		/// <summary>The maximum volume of the rain audio source loop when conditions are fully satisfied.</summary>
		public float RainMaxVolume { set { rainMaxVolume = value; } get { return rainMaxVolume; } } [SerializeField] [Range(0.0f, 1.0f)] private float rainMaxVolume = 1.0f;

		/// <summary>The audio clip used for heavy background ambient thunderstorm rumble loops.</summary>
		public AudioClip ThunderstormClip { set { thunderstormClip = value; } get { return thunderstormClip; } } [SerializeField] private AudioClip thunderstormClip;

		/// <summary>The maximum volume of the thunderstorm audio source loop when conditions are fully satisfied.</summary>
		public float ThunderstormMaxVolume { set { thunderstormMaxVolume = value; } get { return thunderstormMaxVolume; } } [SerializeField] [Range(0.0f, 1.0f)] private float thunderstormMaxVolume = 1.0f;

		/// <summary>The maximum distance from the camera where the thunderstorm loop plays at full volume.</summary>
		public float ThunderstormMaxRange { set { thunderstormMaxRange = value; } get { return thunderstormMaxRange; } } [SerializeField] private float thunderstormMaxRange = 20000.0f;

		/// <summary>The distance over which the thunderstorm volume fades to zero after exceeding the maximum range.</summary>
		public float ThunderstormFalloff { set { thunderstormFalloff = value; } get { return thunderstormFalloff; } } [SerializeField] private float thunderstormFalloff = 5000.0f;

		[SerializeField] private AudioSource rainSource;
		[SerializeField] private AudioSource thunderstormSource;

		[System.NonSerialized] private float currentRainVolumeTarget = 0.0f;
		[System.NonSerialized] private float currentThunderstormVolumeTarget = 0.0f;

		[System.NonSerialized] private SgtCloud cachedCloud;
		[System.NonSerialized] private SgtCloudPrecipitation cachedPrecipitation;
		[System.NonSerialized] private SgtCloudLightning cachedLightning;

		protected virtual void OnEnable()
		{
			SetupAudioSources();

			cachedCloud         = GetComponentInParent<SgtCloud>();
			cachedPrecipitation = GetComponent<SgtCloudPrecipitation>();
			cachedLightning     = GetComponent<SgtCloudLightning>();

			if (cachedCloud != null)
			{
				cachedCloud.OnUpdateWeather += HandleUpdateWeather;
			}
		}

		protected virtual void OnDisable()
		{
			if (cachedCloud != null)
			{
				cachedCloud.OnUpdateWeather -= HandleUpdateWeather;
			}

			if (rainSource != null)             { rainSource.Stop(); }
			if (thunderstormSource != null)     { thunderstormSource.Stop(); }
		}

		private void SetupAudioSources()
		{
			if (rainSource == null)
			{
				rainSource = gameObject.AddComponent<AudioSource>();
				rainSource.loop = true;
				rainSource.playOnAwake = false;
				rainSource.spatialBlend = 0.0f; // Ambient 2D
				rainSource.volume = 0.0f;
			}

			if (thunderstormSource == null)
			{
				thunderstormSource = gameObject.AddComponent<AudioSource>();
				thunderstormSource.loop = true;
				thunderstormSource.playOnAwake = false;
				thunderstormSource.spatialBlend = 0.0f; // Ambient 2D
				thunderstormSource.volume = 0.0f;
			}
		}

		private void HandleUpdateWeather(Bounds bounds, Camera finalCamera)
		{
			if (cachedCloud == null) return;

			var rainIntensity      = 0.0f;
			var snowIntensity      = 0.0f;
			var lightningIntensity = 0.0f;
			var underwaterMul      = Mathf.Clamp01(1.0f - cachedCloud.UnderwaterRatio);

			if (cachedPrecipitation != null)
			{
				cachedPrecipitation.GetWeatherIntensity(finalCamera.transform.position, out rainIntensity, out snowIntensity);
			}

			if (cachedLightning != null)
			{
				var dist = cachedLightning.MainCameraLightningDistance;

				if (dist > 0.0f)
				{
					lightningIntensity = 1.0f - Mathf.Clamp01((dist - thunderstormMaxRange) / thunderstormFalloff);
				}
				else
				{
					lightningIntensity = 0.0f;
				}
			}

			currentRainVolumeTarget         = Mathf.Clamp01(     rainIntensity) * rainMaxVolume * underwaterMul;
			currentThunderstormVolumeTarget = Mathf.Clamp01(lightningIntensity) * thunderstormMaxVolume * underwaterMul;

			UpdateSourceVolumes();
		}

		private void UpdateSourceVolumes()
		{
			if (rainSource != null)
			{
				if (rainSource.clip != rainClip)
				{
					rainSource.clip = rainClip;
					if (rainClip != null && currentRainVolumeTarget > 0.0f)
					{
						rainSource.Play();
						rainSource.time = UnityEngine.Random.Range(0.0f, rainClip.length);
					}
				}

				rainSource.volume = currentRainVolumeTarget;

				if (rainSource.volume > 0.0f && !rainSource.isPlaying && rainSource.clip != null)
				{
					rainSource.Play();
					rainSource.time = UnityEngine.Random.Range(0.0f, rainSource.clip.length);
				}
				else if (rainSource.volume <= 0.0f && rainSource.isPlaying)
				{
					rainSource.Stop();
				}
			}

			if (thunderstormSource != null)
			{
				if (thunderstormSource.clip != thunderstormClip)
				{
					thunderstormSource.clip = thunderstormClip;
					if (thunderstormClip != null && currentThunderstormVolumeTarget > 0.0f)
					{
						thunderstormSource.Play();
						thunderstormSource.time = UnityEngine.Random.Range(0.0f, thunderstormClip.length);
					}
				}

				thunderstormSource.volume = currentThunderstormVolumeTarget;

				if (thunderstormSource.volume > 0.0f && !thunderstormSource.isPlaying && thunderstormSource.clip != null)
				{
					thunderstormSource.Play();
					thunderstormSource.time = UnityEngine.Random.Range(0.0f, thunderstormSource.clip.length);
				}
				else if (thunderstormSource.volume <= 0.0f && thunderstormSource.isPlaying)
				{
					thunderstormSource.Stop();
				}
			}
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Cloud
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtCloudSound))]
	public class SgtCloudSound_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtCloudSound tgt; SgtCloudSound[] tgts; GetTargets(out tgt, out tgts);

			Draw("rainClip", "The audio clip used for environmental rain loop conditions.");
			Draw("rainMaxVolume", "The maximum volume of the rain audio source loop when conditions are fully satisfied.");

			Separator();

			Draw("thunderstormClip", "The audio clip used for heavy background ambient thunderstorm rumble loops.");
			Draw("thunderstormMaxVolume", "The maximum volume of the thunderstorm audio source loop when conditions are fully satisfied.");
			Draw("thunderstormMaxRange", "The maximum distance from the camera where the thunderstorm loop plays at full volume.");
			Draw("thunderstormFalloff", "The distance over which the thunderstorm volume fades to zero after exceeding the maximum range.");
		}
	}
}
#endif