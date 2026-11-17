using UnityEngine;
using CW.Common;
using SpaceGraphicsToolkit.Landscape;
using Unity.Mathematics;

namespace SpaceGraphicsToolkit.Ocean
{
	/// <summary>This component manages environmental shore audio loops, utilizing a distributed Vogel spiral search to dynamically position and blend the audio source between 2D and 3D space based on surrounding shore topography.</summary>
	[RequireComponent(typeof(SgtOcean))]
	[AddComponentMenu("Space Graphics Toolkit/SGT Ocean Sound")]
	public class SgtOceanSound : MonoBehaviour
	{
		/// <summary>The audio clip used for the shoreline wave breaking sound.</summary>
		public AudioClip ShoreClip { set { shoreClip = value; } get { return shoreClip; } } [SerializeField] private AudioClip shoreClip;

		/// <summary>The maximum volume of the shore audio source when conditions are fully satisfied.</summary>
		public float ShoreMaxVolume { set { shoreMaxVolume = value; } get { return shoreMaxVolume; } } [SerializeField] [Range(0.0f, 1.0f)] private float shoreMaxVolume = 1.0f;

		/// <summary>The maximum distance from the camera at which shore sounds can be heard, and the limit for searching for shores.</summary>
		public float MaxSoundRange { set { maxSoundRange = value; } get { return maxSoundRange; } } [SerializeField] private float maxSoundRange = 500.0f;

		/// <summary>The maximum distance the ocean surface can be from the landscape surface to be considered a valid shore.</summary>
		public float ShoreThreshold { set { shoreThreshold = value; } get { return shoreThreshold; } } [SerializeField] private float shoreThreshold = 5.0f;

		/// <summary>The total number of points in the Vogel spiral search pattern to sample around the camera.</summary>
		public int SampleCount { set { sampleCount = value; } get { return sampleCount; } } [SerializeField] private int sampleCount = 128;

		/// <summary>The number of search points to evaluate per frame. Higher values find shores faster but cost more performance.</summary>
		public int SamplesPerFrame { set { samplesPerFrame = value; } get { return samplesPerFrame; } } [SerializeField] private int samplesPerFrame = 4;

		/// <summary>The response speed for sound volume transitions.</summary>
		public float FadeSpeed { set { fadeSpeed = value; } get { return fadeSpeed; } } [SerializeField] private float fadeSpeed = 2.0f;

		[System.NonSerialized] private AudioSource shoreSource;
		[System.NonSerialized] private GameObject shoreGO;
		[System.NonSerialized] private SgtOcean cachedOcean;

		[System.NonSerialized] private float currentShoreVolTarget;
		[System.NonSerialized] private int sampleIndex = 0;
		[System.NonSerialized] private float[] shoreWeights;
		[System.NonSerialized] private Vector3[] shoreVectors;

		// The golden angle in radians used for the Vogel spiral
		private const float GoldenAngle = 2.39996323f; 

		protected virtual void OnEnable()
		{
			cachedOcean = GetComponent<SgtOcean>();
			SetupAudioSource();
		}

		protected virtual void OnDisable()
		{
			if (shoreGO != null) DestroyImmediate(shoreGO);
		}

		protected virtual void OnValidate()
		{
			fadeSpeed       = Mathf.Max(0.001f, fadeSpeed);
			maxSoundRange   = Mathf.Max(1.0f, maxSoundRange);
			sampleCount     = Mathf.Max(1, sampleCount);
			samplesPerFrame = Mathf.Max(1, samplesPerFrame);
		}

		private void SetupAudioSource()
		{
			if (shoreGO == null)
			{
				shoreGO = new GameObject("Shore Sound Source");
				shoreGO.transform.SetParent(transform, false);
				shoreGO.hideFlags = HideFlags.DontSave;
				shoreSource = shoreGO.AddComponent<AudioSource>();
				shoreSource.loop = true;
				shoreSource.playOnAwake = false;
				shoreSource.spatialBlend = 1.0f;
				shoreSource.volume = 0.0f;
				shoreSource.minDistance = 10.0f;
				shoreSource.maxDistance = maxSoundRange;
			}
		}

		private void ValidateArrays()
		{
			if (shoreWeights == null || shoreWeights.Length != sampleCount)
			{
				shoreWeights = new float[sampleCount];
				shoreVectors = new Vector3[sampleCount];
				sampleIndex  = 0;
			}
		}

		protected virtual void Update()
		{
			var finalCamera = cachedOcean.Observer != null ? cachedOcean.Observer : Camera.main;
			if (finalCamera == null)
			{
				currentShoreVolTarget = 0.0f;
				UpdateSourceVolume();
				return;
			}

			ValidateArrays();

			var camPos      = finalCamera.transform.position;
			var localCamPos = transform.InverseTransformPoint(camPos);
			var altitude    = localCamPos.magnitude - cachedOcean.Radius;

			// If camera is underwater, mute ambient shore sound entirely
			if (altitude < 0.0f)
			{
				currentShoreVolTarget = 0.0f;
				UpdateSourceVolume();
				return;
			}

			UpdateAmortizedShoreSearch(camPos, localCamPos);
			ApplyShoreAudioState(camPos);
			UpdateSourceVolume();
		}

		private void UpdateAmortizedShoreSearch(Vector3 camPos, Vector3 localCamPos)
		{
			// Construct a tangent basis mapped flat to the ocean surface relative to the camera
			var up = localCamPos.normalized;
			var right = Vector3.Cross(up, Vector3.up);
			if (right.sqrMagnitude < 0.001f)
			{
				right = Vector3.Cross(up, Vector3.forward);
			}
			right.Normalize();
			var forward = Vector3.Cross(right, up).normalized;

			for (int i = 0; i < samplesPerFrame; i++)
			{
				int n       = sampleIndex;
				float r     = maxSoundRange * Mathf.Sqrt((float)n / Mathf.Max(1, sampleCount - 1));
				float theta = n * GoldenAngle;

				// Map Vogel spiral point onto local ocean tangent plane
				var localTangentOffset = (right * Mathf.Cos(theta) + forward * Mathf.Sin(theta)) * r;
				var testLocal          = (up * cachedOcean.Radius) + localTangentOffset;
				
				// Re-project exactly back onto the perfect sphere radius
				testLocal = testLocal.normalized * cachedOcean.Radius;
				var testWorld = transform.TransformPoint(testLocal);

				float weight = 0f;
				Vector3 vector = Vector3.zero;

				var landscape = SgtLandscape.FindNearest(testWorld);
				if (landscape != null)
				{
					var terrainWorldPoint = (Vector3)(float3)landscape.GetWorldPoint((float3)testWorld);
					var heightOffset      = Vector3.Distance(testWorld, terrainWorldPoint);

					if (heightOffset < shoreThreshold)
					{
						var distToCam = Vector3.Distance(terrainWorldPoint, camPos);
						if (distToCam < maxSoundRange)
						{
							weight = 1.0f - (distToCam / maxSoundRange);
							vector = (terrainWorldPoint - camPos).normalized;
						}
					}
				}

				// Cache the vector scaled by its weight. A closer shore pulls the average harder.
				shoreWeights[n] = weight;
				shoreVectors[n] = vector * weight;

				sampleIndex = (sampleIndex + 1) % sampleCount;
			}
		}

		private void ApplyShoreAudioState(Vector3 camPos)
		{
			float maxWeight = 0f;
			float sumWeight = 0f;
			Vector3 sumVector = Vector3.zero;

			for (int i = 0; i < sampleCount; i++)
			{
				maxWeight  = Mathf.Max(maxWeight, shoreWeights[i]);
				sumWeight += shoreWeights[i];
				sumVector += shoreVectors[i];
			}

			if (maxWeight > 0.001f)
			{
				// Determine average direction. If shores surround us uniformly, sumVector nears zero.
				var avgVector = sumVector / sumWeight;
				
				// Magnitude dictates 2D vs 3D representation
				shoreSource.spatialBlend = Mathf.Clamp01(avgVector.magnitude);

				if (avgVector.sqrMagnitude > 0.001f)
				{
					// Position the source somewhere along the dominant shore direction line
					float simulatedDist = maxSoundRange * (1.0f - maxWeight);
					shoreGO.transform.position = camPos + avgVector.normalized * simulatedDist;
				}
				else
				{
					shoreGO.transform.position = camPos;
				}

				// The closest detected shore point dictates the absolute volume
				currentShoreVolTarget = shoreMaxVolume * maxWeight;
			}
			else
			{
				currentShoreVolTarget = 0.0f;
			}
		}

		private void UpdateSourceVolume()
		{
			if (shoreSource != null)
			{
				if (shoreSource.clip != shoreClip)
				{
					shoreSource.clip = shoreClip;
					if (shoreClip != null && currentShoreVolTarget > 0.0f)
					{
						shoreSource.Play();
						shoreSource.time = UnityEngine.Random.Range(0.0f, shoreClip.length);
					}
				}

				shoreSource.volume = Mathf.MoveTowards(shoreSource.volume, currentShoreVolTarget, fadeSpeed * Time.deltaTime);

				if (shoreSource.volume > 0.0f && !shoreSource.isPlaying && shoreSource.clip != null)
				{
					shoreSource.Play();
					shoreSource.time = UnityEngine.Random.Range(0.0f, shoreSource.clip.length);
				}
				else if (shoreSource.volume <= 0.0f && shoreSource.isPlaying)
				{
					shoreSource.Stop();
				}
			}
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Ocean
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtOceanSound))]
	public class SgtOceanSound_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtOceanSound tgt; SgtOceanSound[] tgts; GetTargets(out tgt, out tgts);

			Draw("shoreClip", "The audio clip used for the shoreline wave breaking sound.");
			Draw("shoreMaxVolume", "The maximum volume of the shore audio source when conditions are fully satisfied.");
			
			Separator();

			Draw("maxSoundRange", "The maximum distance from the camera at which shore sounds can be heard, and the limit for searching for shores.");
			Draw("shoreThreshold", "The maximum distance the ocean surface can be from the landscape surface to be considered a valid shore.");
			
			Separator();
			
			Draw("sampleCount", "The total number of points in the Vogel spiral search pattern to sample around the camera.");
			Draw("samplesPerFrame", "The number of search points to evaluate per frame. Higher values find shores faster but cost more performance.");
			Draw("fadeSpeed", "The response speed for sound volume transitions.");
		}
	}
}
#endif