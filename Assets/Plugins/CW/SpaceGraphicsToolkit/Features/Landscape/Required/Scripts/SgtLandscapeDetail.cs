using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>This feature can modify the height of a terrain in a specific area.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Landscape Detail")]
	public class SgtLandscapeDetail : SgtLandscapeFeature
	{
		public enum SpaceType
		{
			Global,
			Local
		}

		/// <summary>Where should the detail be applied?
		/// Global = The whole landscape.
		/// Local = A small section of the landscape defined by the <b>Transform</b> component's position, rotation, and scale.</summary>
		public SpaceType Space { set { space = value; } get { return space; } } [SerializeField] private SpaceType space;

		/// <summary>The <b>HeightRange</b> and <b>GlobalSize</b> settings will be multiplied by this amount. This is useful if you're changing your planet size and want your detail to match.</summary>
		public float SurfaceScale { set { surfaceScale = value; } get { return surfaceScale; } } [SerializeField] private float surfaceScale = 1.0f;

		/// <summary>The mask texture used by this component.</summary>
		public Texture2D Mask { set { mask = value; } get { return mask; } } [SerializeField] protected Texture2D mask;

		/// <summary>This allows you to invert the mask.</summary>
		public bool MaskInvert { set { maskInvert = value; } get { return maskInvert; } } [SerializeField] private bool maskInvert;

		/// <summary>This allows you to set how sharp or smooth the mask transition is.</summary>
		public float MaskSharpness { set { maskSharpness = value; } get { return maskSharpness; } } [SerializeField] [Range(1.0f, 100.0f)] private float maskSharpness = 1.0f;

		/// <summary>This allows you to offset the mask based on the topology.</summary>
		public float MaskGlobalShift { set { maskGlobalShift = value; } get { return maskGlobalShift; } } [SerializeField] [Range(-1.0f, 1.0f)] private float maskGlobalShift;

		/// <summary>This allows you to specify which detail texture will be used.</summary>
		public Texture2D MaskDetail { set { maskDetail = value; } get { return maskDetail; } } [SerializeField] private Texture2D maskDetail;

		/// <summary>This allows you to adjust the mask detail texture tiling.</summary>
		public Vector2 MaskDetailTiling { set { maskDetailTiling = value; } get { return maskDetailTiling; } } [SerializeField] private Vector2 maskDetailTiling = new Vector2(10.0f, 10.0f);

		/// <summary>This allows you to adjust the mask detail texture tiling.</summary>
		public float MaskDetailOffset { set { maskDetailOffset = value; } get { return maskDetailOffset; } } [SerializeField] [Range(0.0001f, 0.1f)] private float maskDetailOffset = 0.1f;

		/// <summary>This allows you to specify which height texture the detail uses.</summary>
		public Texture2D HeightTexture { set { heightTexture = value; } get { return heightTexture; } } [SerializeField] protected Texture2D heightTexture;

		/// <summary>This allows you to define where in the heightmap the height is 0. For example, if you want this heightmap to go down into the existing terrain, then you must increase this to where the ground is supposed to be flat.</summary>
		public float HeightMidpoint { set { heightMidpoint = value; } get { return heightMidpoint; } } [SerializeField] [Range(0.0f, 1.0f)] protected float heightMidpoint;

		/// <summary>This allows you define the difference in height between the lowest and highest points.</summary>
		public float HeightRange { set { heightRange = value; } get { return heightRange; } } [SerializeField] protected float heightRange = 1.0f;

		/// <summary>The detail will be tiled this many times around the landscape.</summary>
		public Vector2 LocalTiling { set { localTiling = value; } get { return localTiling; } } [SerializeField] protected Vector2 localTiling = new Vector2(1.0f, 1.0f);

		/// <summary>This allows you to adjust how deep the heightmap penetrates into the terrain texture when it gets colored.</summary>
		public float Strata { set { strata = value; } get { return strata; } } [SerializeField] protected float strata = 1.0f;

		/// <summary>This allows you to specify the size of this layer of detail, which is used to calculate the tiling.</summary>
		public float GlobalSize { set { globalSize = value; } get { return globalSize; } } [SerializeField] protected float globalSize = 1024;

		/// <summary>If you enable this, then this heightmap will modify the actual mesh geometry height values. If not, it will just be a visual effect like a normal map.</summary>
		public bool Displace { set { displace = value; } get { return displace; } } [SerializeField] protected bool displace = true;

		/// <summary>If you enable this, then this heightmap will replace/override any previously written height data.</summary>
		public bool Replace { set { replace = value; } get { return replace; } } [SerializeField] protected bool replace;

		[System.NonSerialized] private double4x4 matrix;

		[System.NonSerialized] private float2 localScale; public float2 LocalScale { get { return localScale; } }

		[System.NonSerialized] private double globalTiling;

		[System.NonSerialized] private int globalIndex; public int GlobalIndex { get { return globalIndex; } }

		[System.NonSerialized] private int globalTile; public int GlobalTile { get { return globalTile; } }

		// OLD
		[SerializeField] private int maskIndex; public int MaskIndex { get { return maskIndex; } }
		[SerializeField] private int maskDetailIndex; public int MaskDetailIndex { get { return maskDetailIndex; } }
		[SerializeField] private int heightIndex; public int HeightIndex { get { return heightIndex; } }

		[BurstCompile]
		public struct GlobalJob : IJob
		{
			[ReadOnly] public NativeArray<double4> Coords;
			[ReadOnly] public NativeArray<double4> DataA;
			[ReadOnly] public NativeArray<double4> DataB;
			           public NativeArray<double>  Heights;

			[ReadOnly] public NativeArray<byte  > HeightData08;
			[ReadOnly] public NativeArray<ushort> HeightData16;
			[ReadOnly] public int2                HeightSize;
			[ReadOnly] public double2             HeightRange;

			[ReadOnly] public NativeArray<byte  > MaskData08;
			[ReadOnly] public NativeArray<ushort> MaskData16;
			[ReadOnly] public int2                MaskSize;
			[ReadOnly] public float               MaskShift;
			[ReadOnly] public float               MaskInvert;

			[ReadOnly] public double2 Tiling;
			[ReadOnly] public float   Replace;

			private void Contribute16(int i, double2 coord, double weight)
			{
				//var height = SgtLandscape.Sample_Cubic(HeightData16, HeightSize, coord * Tiling * HeightSize);
				var height = SgtLandscape.Sample_Linear(HeightData16, HeightSize, coord * Tiling * HeightSize);

				Heights[i] += (HeightRange.x + HeightRange.y * height) * weight;
			}

			private void Contribute08(int i, double2 coord, double weight)
			{
				//var height = SgtLandscape.Sample_Cubic(HeightData08, HeightSize, coord * Tiling * HeightSize);
				var height = SgtLandscape.Sample_Linear(HeightData08, HeightSize, coord * Tiling * HeightSize);

				Heights[i] += (HeightRange.x + HeightRange.y * height) * weight;
			}

			private float GetMask(double2 coord, double2 coordOffset)
			{
				coord += coordOffset * MaskShift;

				var mask = 1.0f;

				if (MaskData16.Length > 0)
				{
					mask = SgtLandscape.Sample_Linear_Clamp(MaskData16, MaskSize, coord * MaskSize); mask = math.lerp(mask, 1.0f - mask, MaskInvert);
				}
				else if (MaskData08.Length > 0)
				{
					mask = SgtLandscape.Sample_Linear_Clamp(MaskData08, MaskSize, coord * MaskSize); mask = math.lerp(mask, 1.0f - mask, MaskInvert);
				}

				return mask;
			}

			public void Execute()
			{
				var two = DataA.Length > 0 && DataA[0].z >= 0.0;

				if (HeightData16.Length > 0)
				{
					if (two == true)
					{
						for (var i = 0; i < Heights.Length; i++)
						{
							var o = GetMask(DataA[i].xy, DataB[i].xy);

							Heights[i] = math.lerp(Heights[i], 0.0f, Replace * o);

							Contribute16(i, Coords[i].xy, DataA[i].z * o);
							Contribute16(i, Coords[i].zw, DataA[i].w * o);
						}
					}
					else
					{
						for (var i = 0; i < Heights.Length; i++)
						{
							var o = GetMask(DataA[i].xy, DataB[i].xy);

							Heights[i] = math.lerp(Heights[i], 0.0f, Replace * o);

							Contribute16(i, Coords[i].xy, o);
						}
					}
				}
				else if (HeightData08.Length > 0)
				{
					if (two == true)
					{
						for (var i = 0; i < Heights.Length; i++)
						{
							var o = GetMask(DataA[i].xy, DataB[i].xy);

							Heights[i] = math.lerp(Heights[i], 0.0f, Replace * o);

							Contribute08(i, Coords[i].xy, DataA[i].z * o);
							Contribute08(i, Coords[i].zw, DataA[i].w * o);
						}
					}
					else
					{
						for (var i = 0; i < Heights.Length; i++)
						{
							var o = GetMask(DataA[i].xy, DataB[i].xy);

							Heights[i] = math.lerp(Heights[i], 0.0f, Replace * o);

							Contribute08(i, Coords[i].xy, o);
						}
					}
				}
			}
		}

		[BurstCompile]
		public struct LocalJob : IJob
		{
			[ReadOnly] public NativeArray<double3> Points;
			           public NativeArray<double>  Heights;

			[ReadOnly] public NativeArray<byte  > HeightData08;
			[ReadOnly] public NativeArray<ushort> HeightData16;
			[ReadOnly] public int2                HeightSize;
			[ReadOnly] public double2             HeightRange;

			[ReadOnly] public NativeArray<byte  > MaskData08;
			[ReadOnly] public NativeArray<ushort> MaskData16;
			[ReadOnly] public int2                MaskSize;
			[ReadOnly] public float               MaskInvert;

			[ReadOnly] public double4x4 Matrix;
			[ReadOnly] public double2   Tiling;
			[ReadOnly] public float     Replace;

			private float GetMask(double2 coord)
			{
				var mask = 1.0f;

				if (MaskData16.Length > 0)
				{
					mask = SgtLandscape.Sample_Linear_Clamp(MaskData16, MaskSize, coord * MaskSize); mask = math.lerp(mask, 1.0f - mask, MaskInvert);
				}
				else if (MaskData08.Length > 0)
				{
					mask = SgtLandscape.Sample_Linear_Clamp(MaskData08, MaskSize, coord * MaskSize); mask = math.lerp(mask, 1.0f - mask, MaskInvert);
				}

				return mask;
			}

			public void Execute()
			{
				if (HeightData16.Length > 0)
				{
					for (var i = 0; i < Points.Length; i++)
					{
						var point = math.mul(Matrix, new double4(Points[i], 1.0)).xyz;

						if (point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1 && point.z >= 0 && point.z <= 1)
						{
							var h = HeightRange.x + HeightRange.y * SgtLandscape.Sample_Cubic(HeightData16, HeightSize, point.xy * Tiling);
							var o = GetMask(point.xy);

							Heights[i] = math.lerp(Heights[i], 0.0, Replace * o) + h * o;
						}
					}
				}
				else if (HeightData08.Length > 0)
				{
					for (var i = 0; i < Points.Length; i++)
					{
						var point = math.mul(Matrix, new double4(Points[i], 1.0)).xyz;

						if (point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1 && point.z >= 0 && point.z <= 1)
						{
							var h = HeightRange.x + HeightRange.y * SgtLandscape.Sample_Cubic(HeightData08, HeightSize, point.xy * Tiling);
							var o = GetMask(point.xy);

							Heights[i] = math.lerp(Heights[i], 0.0, Replace * o) + h * o;
						}
					}
				}
			}
		}

		public static SgtLandscapeDetail Create(int layer = 0, Transform parent = null)
		{
			return Create(layer, parent, Vector3.zero, Quaternion.identity, Vector3.one);
		}

		public static SgtLandscapeDetail Create(int layer, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
		{
			return CW.Common.CwHelper.CreateGameObject("SgtLandscapeDetail", layer, parent, localPosition, localRotation, localScale).AddComponent<SgtLandscapeDetail>();
		}

		public override void RegisterTextures()
		{
			SgtLandscapeTextures.RegisterHeight(heightTexture);
			SgtLandscapeTextures.RegisterMask(mask);
			SgtLandscapeTextures.RegisterDetail(maskDetail);
		}

		public override void Prepare()
		{
			matrix = CalculateMatrix(transform.localPosition, transform.localRotation, transform.localScale);

			localScale = CalculateScale(localTiling, heightRange);

			var index = default(int);
			var tile  = default(int);

			if (cachedLandscape.GetTilingLayer(globalSize * surfaceScale, ref index, ref tile) == true)
			{
				globalTiling = cachedLandscape.GlobalTiling[index] * tile;
				globalIndex  = index;
				globalTile   = tile;
			}
			else
			{
				globalTiling = 0.0;
				globalIndex  = 0;
				globalTile   = 0;
			}
		}

		public override void Dispose()
		{
		}

		public override void ScheduleCpu(SgtLandscape.PendingPoints pending)
		{
			if (displace == true)
			{
				var heightData = SgtLandscapeTextures.GetHeightData(heightTexture);
				var maskData   = SgtLandscapeTextures.GetMaskData(mask);

				if (space == SpaceType.Global)
				{
					if (globalTile > 0)
					{
						var job = new GlobalJob();

						job.Coords  = pending.Coords;
						job.DataA   = pending.DataA;
						job.DataB   = pending.DataB;
						job.Heights = pending.Heights;

						job.HeightData08 = heightData.Data08;
						job.HeightData16 = heightData.Data16;
						job.HeightSize   = heightData.Size;
						job.HeightRange  = new float2(-heightRange * heightMidpoint, heightRange) * surfaceScale;

						job.MaskData08 = maskData.Data08;
						job.MaskData16 = maskData.Data16;
						job.MaskSize   = maskData.Size;
						job.MaskShift  = maskGlobalShift;
						job.MaskInvert = maskInvert == true ? 1.0f : 0.0f;

						job.Tiling  = globalTiling;
						job.Replace = replace == true ? 1.0f : 0.0f;

						pending.Handle = job.Schedule(pending.Handle);
					}
				}
				else
				{
					var job = new LocalJob();

					job.Points  = pending.Points;
					job.Heights = pending.Heights;

					job.HeightData08 = heightData.Data08;
					job.HeightData16 = heightData.Data16;
					job.HeightSize   = heightData.Size;
					job.HeightRange  = new float2(-heightRange * heightMidpoint, heightRange) * surfaceScale;

					job.MaskData08 = maskData.Data08;
					job.MaskData16 = maskData.Data16;
					job.MaskSize   = maskData.Size;
					job.MaskInvert = maskInvert == true ? 1.0f : 0.0f;

					job.Matrix  = matrix;
					job.Tiling  = (float2)localTiling;
					job.Replace = replace == true ? 1.0f : 0.0f;

					pending.Handle = job.Schedule(pending.Handle);
				}
			}
		}

#if UNITY_EDITOR
		protected virtual void OnDrawGizmos()
		{
			if (space == SpaceType.Local)
			{
				var m = transform.localToWorldMatrix;

				Gizmos.matrix = m;

				Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.0f, 1.0f, 0.0f));
				Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.0f, 1.0f, 1.0f));

				if (mask != null)
				{
					for (var i = 0; i < 16; i++)
					{
						var subMatrix = m * Matrix4x4.Translate(new Vector3(0.0f, 0.0f, Mathf.Lerp(-0.5f, 0.5f, i / 15.0f)));

						CW.Common.CwHelper.DrawShapeOutline(mask, 0, subMatrix);
					}
				}
			}
		}
#endif
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Landscape
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtLandscapeDetail))]
	public class SgtLandscapeDetail_Editor : SgtLandscapeFeature_Editor
	{
		protected override void OnInspector()
		{
			SgtLandscapeDetail tgt; SgtLandscapeDetail[] tgts; GetTargets(out tgt, out tgts);

			var markForRebuild = false;

			Draw("space", ref markForRebuild, "Where should the detail be applied?\n\nGlobal = The whole landscape.\n\nLocal = A small section of the landscape defined by the <b>Transform</b> component's position, rotation, and scale.");
			BeginError(Any(tgts, t => t.SurfaceScale <= 0.0f));
				Draw("surfaceScale", ref markForRebuild, "The <b>HeightRange</b> and <b>GlobalSize</b> settings will be multiplied by this amount. This is useful if you're changing your planet size and want your detail to match.");
			EndError();
			Draw("mask", ref markForRebuild, "The mask texture used by this component.");

			if (Any(tgts, t => t.Mask != null))
			{
				BeginIndent();
					Draw("maskInvert", ref markForRebuild, "This allows you to invert the mask.");
					Draw("maskSharpness", ref markForRebuild, "This allows you to set how sharp or smooth the mask transition is.");
					Draw("maskGlobalShift", ref markForRebuild, "This allows you to offset the mask based on the topology.");
					Draw("maskDetail", ref markForRebuild, "This allows you to enhance the detail around the edges of the mask.");

					if (Any(tgts, t => t.MaskDetail != null))
					{
						Draw("maskDetailTiling", ref markForRebuild, "This allows you to adjust the mask detail texture tiling.");
						Draw("maskDetailOffset", ref markForRebuild, "This allows you to maximum amount the mask can be shifted based on the <b>MaskDetail</b> texture in UV space.");
					}
				EndIndent();
			}

			Separator();

			Draw("heightTexture", ref markForRebuild, "This allows you to specify which height texture the detail uses.");
			Draw("heightMidpoint", ref markForRebuild, "This allows you to define where in the heightmap the height is 0. For example, if you want this heightmap to go down into the existing terrain, then you must increase this to where the ground is supposed to be flat.");
			Draw("heightRange", ref markForRebuild, "This allows you define the difference in height between the lowest and highest points.");
			Draw("strata", ref markForRebuild, "This allows you to adjust how deep the heightmap penetrates into the terrain texture when it gets colored.");

			if (Any(tgts, t => t.Space == SgtLandscapeDetail.SpaceType.Global))
			{
				Draw("globalSize", ref markForRebuild, "This allows you to specify the size of this layer of detail, which is used to calculate the tiling.");
			}
			if (Any(tgts, t => t.Space == SgtLandscapeDetail.SpaceType.Local))
			{
				Draw("localTiling", ref markForRebuild, "The detail will be tiled this many times around the landscape.");
			}
			Draw("displace", ref markForRebuild, "If you enable this, then this heightmap will modify the actual mesh geometry height values. If not, it will just be a visual effect like a normal map.");
			Draw("replace", ref markForRebuild, "If you enable this, then this heightmap will replace/override any previously written height data.");

			if (Any(tgts, t => t.Mask == true) && Button("Randomize MaskTexture") == true)
			{
				Each(tgts, t => t.Mask = SgtLandscape_Editor.Randomize(t.Mask, ref markForRebuild), true);
			}

			if (Button("Randomize HeightTexture") == true)
			{
				Each(tgts, t => t.HeightTexture = SgtLandscape_Editor.Randomize(t.HeightTexture, ref markForRebuild), true);
			}

			if (markForRebuild == true)
			{
				Each(tgts, t => t.MarkForRebuild());
			}
		}

		protected override void OnTransformChanged()
		{
			var tgt = (SgtLandscapeDetail)target;

			if (tgt.Space == SgtLandscapeDetail.SpaceType.Local)
			{
				tgt.MarkForRebuild();
			}
		}
	}
}
#endif