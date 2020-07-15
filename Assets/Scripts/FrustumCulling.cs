//https://gist.github.com/StagPoint/4d8ca93923f66ad60ce480124c0d5092

using System;
using System.Collections.Generic;
using UnityEngine;

public class FrustumCulling : MonoBehaviour
{
	public struct CullingBox
	{
		public Vector3 boxCenter;
		public Vector3 boxExtents;
		public bool isInFrustum;
		public Renderer renderer;
	}

	private Camera currentCamera;

	/// <summary>
	/// The number of planes in the frustum.
	/// </summary>
	private const int planeCount = 6;

	/// <summary>
	/// The number of corner points in the frustum.
	/// </summary>
	private const int cornerCount = 8;

	/// <summary>
	/// Ordering: [0] = Far Bottom Left, [1] = Far Top Left, [2] = Far Top Right, [3] = Far Bottom Right, 
	/// [4] = Near Bottom Left, [5] = Near Top Left, [6] = Near Top Right, [7] = Near Bottom Right
	/// </summary>
	private Vector3[] corners = new Vector3[cornerCount];

	/// <summary>
	/// Defines the set of planes that bound the camera's frustum. All plane normals point to the inside of the 
	/// frustum.
	/// Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
	/// </summary>
	private Plane[] planes = new Plane[planeCount];

	/// <summary>
	/// Caches the absolute values of plane normals for re-use during frustum culling of multiple AABB instances
	/// </summary>
	private Vector3[] absNormals = new Vector3[planeCount];

	/// <summary>
	/// Caching the plane normals allows the culling code to avoid calling property getters on the Plane instances
	/// </summary>
	private Vector3[] planeNormal = new Vector3[planeCount];

	/// <summary>
	/// Caching the plane distances allows the culling code to avoid calling property getters on the Plane instances
	/// </summary>
	private float[] planeDistance = new float[planeCount];

	private CullingBox[] cullingBoxes;

	private void Start()
    {
		currentCamera = Camera.main;

		SpawnBoxes(1000, -20f, 20f, -20f, 20f, -20f, 20f);
	}

    private void Update()
    {
		Transform camTransform = currentCamera.transform;
		Vector3 position = camTransform.position;
		Quaternion orientation = camTransform.rotation;
		Vector3 forward = orientation * Vector3.forward;

		CalculateFrustumCornersPerspective(
			ref position,
			ref orientation,
			currentCamera.fieldOfView,
			currentCamera.nearClipPlane,
			currentCamera.farClipPlane,
			currentCamera.aspect
		);

		planes[0] = new Plane(corners[4], corners[1], corners[0]);
		planes[1] = new Plane(corners[6], corners[3], corners[2]);
		planes[2] = new Plane(corners[7], corners[0], corners[3]);
		planes[3] = new Plane(corners[5], corners[2], corners[1]);
		planes[4] = new Plane(forward, position + forward * currentCamera.nearClipPlane);
		planes[5] = new Plane(-forward, position + forward * currentCamera.farClipPlane);

		for (int i = 0; i < planeCount; i++)
		{
			var plane = planes[i];
			var normal = plane.normal;

			absNormals[i] = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
			planeNormal[i] = normal;
			planeDistance[i] = plane.distance;
		}

		CullBoxes(cullingBoxes);
	}

    private void CalculateFrustumCornersPerspective(ref Vector3 position, ref Quaternion orientation, float fov, float nearClipPlane, float farClipPlane, float aspect)
	{
		float fovWHalf = fov * 0.5f;

		Vector3 toRight = Vector3.right * Mathf.Tan(fovWHalf * Mathf.Deg2Rad) * aspect;
		Vector3 toTop = Vector3.up * Mathf.Tan(fovWHalf * Mathf.Deg2Rad);
		var forward = Vector3.forward;

		Vector3 topLeft = (forward - toRight + toTop);
		float camScale = topLeft.magnitude * farClipPlane;

		topLeft.Normalize();
		topLeft *= camScale;

		Vector3 topRight = (forward + toRight + toTop);
		topRight.Normalize();
		topRight *= camScale;

		Vector3 bottomRight = (forward + toRight - toTop);
		bottomRight.Normalize();
		bottomRight *= camScale;

		Vector3 bottomLeft = (forward - toRight - toTop);
		bottomLeft.Normalize();
		bottomLeft *= camScale;

		corners[0] = position + orientation * bottomLeft;
		corners[1] = position + orientation * topLeft;
		corners[2] = position + orientation * topRight;
		corners[3] = position + orientation * bottomRight;

		topLeft = (forward - toRight + toTop);
		camScale = topLeft.magnitude * nearClipPlane;

		topLeft.Normalize();
		topLeft *= camScale;

		topRight = (forward + toRight + toTop);
		topRight.Normalize();
		topRight *= camScale;

		bottomRight = (forward + toRight - toTop);
		bottomRight.Normalize();
		bottomRight *= camScale;

		bottomLeft = (forward - toRight - toTop);
		bottomLeft.Normalize();
		bottomLeft *= camScale;

		corners[4] = position + orientation * bottomLeft;
		corners[5] = position + orientation * topLeft;
		corners[6] = position + orientation * topRight;
		corners[7] = position + orientation * bottomRight;
	}

	/// <summary>
	/// Returns true if the frustum contains the specified point
	/// </summary>
	public bool Contains(ref Vector3 point)
	{
		for (int i = 0; i < planeCount; i++)
		{
			var normal = planeNormal[i];
			var distance = planeDistance[i];

			float dist = normal.x * point.x + normal.y * point.y + normal.z * point.z + distance;

			if (dist < 0f)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Iterates through each box in the boxes array and sets the Result field to the result of the box/frustum intersection test.
	/// This function is intended primarily for use with static geometry (or quadtrees, etc) where the bounding volumes will not 
	/// be updated frequently, but the frustum will. 
	/// </summary>
	public void CullBoxes(CullingBox[] boxes)
	{
		var abs0 = absNormals[0];
		var abs1 = absNormals[1];
		var abs2 = absNormals[2];
		var abs3 = absNormals[3];
		var abs4 = absNormals[4];
		var abs5 = absNormals[5];

		var planeNormal0 = planeNormal[0];
		var planeNormal1 = planeNormal[1];
		var planeNormal2 = planeNormal[2];
		var planeNormal3 = planeNormal[3];
		var planeNormal4 = planeNormal[4];
		var planeNormal5 = planeNormal[5];

		var planeDistance0 = planeDistance[0];
		var planeDistance1 = planeDistance[1];
		var planeDistance2 = planeDistance[2];
		var planeDistance3 = planeDistance[3];
		var planeDistance4 = planeDistance[4];
		var planeDistance5 = planeDistance[5];

		for (int bi = 0; bi < boxes.Length; bi++)
		{
			var box = boxes[bi];
			var center = box.boxCenter;
			var extents = box.boxExtents;

			bool outOfFrustum = false;

			outOfFrustum = outOfFrustum || (
				(extents.x * abs0.x + extents.y * abs0.y + extents.z * abs0.z) +
				(planeNormal0.x * center.x + planeNormal0.y * center.y + planeNormal0.z * center.z)) < -planeDistance0;

			outOfFrustum = outOfFrustum || (
				(extents.x * abs1.x + extents.y * abs1.y + extents.z * abs1.z) +
				(planeNormal1.x * center.x + planeNormal1.y * center.y + planeNormal1.z * center.z)) < -planeDistance1;

			outOfFrustum = outOfFrustum || (
				(extents.x * abs2.x + extents.y * abs2.y + extents.z * abs2.z) +
				(planeNormal2.x * center.x + planeNormal2.y * center.y + planeNormal2.z * center.z)) < -planeDistance2;

			outOfFrustum = outOfFrustum || (
				(extents.x * abs3.x + extents.y * abs3.y + extents.z * abs3.z) +
				(planeNormal3.x * center.x + planeNormal3.y * center.y + planeNormal3.z * center.z)) < -planeDistance3;

			outOfFrustum = outOfFrustum || (
				(extents.x * abs4.x + extents.y * abs4.y + extents.z * abs4.z) +
				(planeNormal4.x * center.x + planeNormal4.y * center.y + planeNormal4.z * center.z)) < -planeDistance4;

			outOfFrustum = outOfFrustum || (
				(extents.x * abs5.x + extents.y * abs5.y + extents.z * abs5.z) +
				(planeNormal5.x * center.x + planeNormal5.y * center.y + planeNormal5.z * center.z)) < -planeDistance5;

			boxes[bi].isInFrustum = !outOfFrustum;
			boxes[bi].renderer.enabled = boxes[bi].isInFrustum;
		}
	}

	private void SpawnBoxes(int count, float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
	{
		System.Random rnd = new System.Random(Guid.NewGuid().GetHashCode());
		cullingBoxes = new CullingBox[count];

        for (int i = 0; i < count; i++)
        {
			GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
			obj.transform.position = new Vector3(Remap((float)rnd.NextDouble(), 0f, 1f, minX, maxX),
												 Remap((float)rnd.NextDouble(), 0f, 1f, minY, maxY),
												 Remap((float)rnd.NextDouble(), 0f, 1f, minZ, maxZ));
			Destroy(obj.GetComponent<Collider>());
			Mesh mesh = obj.GetComponent<MeshFilter>().mesh;
			cullingBoxes[i] = new CullingBox()
			{
				boxCenter = mesh.bounds.center + obj.transform.position,
				boxExtents = mesh.bounds.extents,
				isInFrustum = false,
				renderer = obj.GetComponent<Renderer>()
			};
        }
    }	

	private float Remap(float value, float inputFrom, float inputTo, float outputFrom, float outputTo)
	{
		return (value - inputFrom) / (inputTo - inputFrom) * (outputTo - outputFrom) + outputFrom;
	}
}
