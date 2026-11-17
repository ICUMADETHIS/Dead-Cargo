using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>This component holds the mesh and entrance data for a cave prefab used by SgtLandscapeCave.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Landscape Cave Data")]
	public class SgtLandscapeCaveData : MonoBehaviour
	{
		public enum ShapeType
		{
			Circle,
			Box
		}

		[System.Serializable]
		public class Entrance
		{
			/// <summary>The shape of this cave entrance.</summary>
			public ShapeType Shape { set { shape = value; } get { return shape; } } [SerializeField] private ShapeType shape;

			/// <summary>The radius of this cave entrance in local space.</summary>
			public float Radius { set { radius = value; } get { return radius; } } [SerializeField] private float radius = 1.0f;

			/// <summary>The size of this cave entrance in local space. This will be multiplied by Radius.</summary>
			public Vector3 Size { set { size = value; } get { return size; } } [SerializeField] private Vector3 size = new Vector3(1.0f, 5.0f, 1.0f);

			/// <summary>The local position offset of this entrance relative to the cave root.</summary>
			public Vector3 Position { set { position = value; } get { return position; } } [SerializeField] private Vector3 position;

			/// <summary>The local rotation offset (Euler angles) of this entrance relative to the cave root.</summary>
			public Vector3 Rotation { set { rotation = value; } get { return rotation; } } [SerializeField] private Vector3 rotation;

			[System.NonSerialized] public double4x4 EntranceMatrix;
		}

		/// <summary>The collection of entrances associated with this cave system.</summary>
		public List<Entrance> Entrances { get { if (entrances == null) entrances = new List<Entrance>(); return entrances; } } [SerializeField] private List<Entrance> entrances;

		/// <summary>The mesh filters within the root that should be modified. These meshes should have an opening at the top (Y=0).</summary>
		public List<MeshFilter> DeformVisuals { get { if (deformVisuals == null) deformVisuals = new List<MeshFilter>(); return deformVisuals; } } [SerializeField] private List<MeshFilter> deformVisuals;

		public void PopulateDeformVisuals()
		{
			if (deformVisuals == null)
			{
				deformVisuals = new List<MeshFilter>();
			}
			
			deformVisuals.Clear();

			var mfs = GetComponentsInChildren<MeshFilter>(true);

			foreach (var mf in mfs)
			{
				if (mf.sharedMesh != null && deformVisuals.Contains(mf) == false)
				{
					deformVisuals.Add(mf);
				}
			}
		}

#if UNITY_EDITOR
		protected virtual void OnDrawGizmosSelected()
		{
			if (entrances == null) return;

			Gizmos.color = Color.cyan;
			foreach (var entrance in entrances)
			{
				if (entrance == null) continue;

				Matrix4x4 localMatrix = Matrix4x4.TRS(entrance.Position, Quaternion.Euler(entrance.Rotation), entrance.Size * entrance.Radius);
				Gizmos.matrix = transform.localToWorldMatrix * localMatrix;

				if (entrance.Shape == ShapeType.Circle) Gizmos.DrawWireSphere(Vector3.zero, 1.0f);
				if (entrance.Shape == ShapeType.Box)    Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 2.0f);
			}
		}
#endif
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Landscape
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtLandscapeCaveData))]
	public class SgtLandscapeCaveData_Editor : CW.Common.CwEditor
	{
		protected override void OnInspector()
		{
			SgtLandscapeCaveData tgt; SgtLandscapeCaveData[] tgts; GetTargets(out tgt, out tgts);

			Draw("entrances", "The entrances to the cave.");

			Separator();

			Draw("deformVisuals", "The mesh filters within the root that should be modified.");

			if (Any(tgts, t => t != null) && Button("Populate from Root") == true)
			{
				foreach (var t in tgts)
				{
					t.PopulateDeformVisuals();
					UnityEditor.EditorUtility.SetDirty(t);
				}
			}
		}
	}
}
#endif