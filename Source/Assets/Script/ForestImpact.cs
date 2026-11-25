using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ForestImpact : MonoBehaviour
{
    private struct Fragment
    {
        public Transform Object;
        public Vector3 Velocity;
        public float Lifetime;
        public bool Dust;
    }

    private readonly List<Fragment> fragments = new();
    private Material crimson;
    private Material scarlet;
    private Material earth;
    private float ground;

    public void Initialize(Material reference, float groundHeight)
    {
        ground = groundHeight;
        crimson = Tint(reference, new Color(0.58f, 0.035f, 0.055f));
        scarlet = Tint(reference, new Color(0.94f, 0.065f, 0.11f));
        earth = Tint(reference, new Color(0.46f, 0.29f, 0.17f));
    }

    public void Blood(Vector3 point, Vector3 travel)
    {
        Vector3 spray = -travel.normalized * 0.7f;
        for (int index = 0; index < 13; index++)
        {
            float size = Random.Range(0.028f, 0.073f);
            Vector3 velocity = spray + Random.insideUnitSphere * 1.8f + Vector3.up * Random.Range(0.3f, 2f);
            Spawn(point, velocity, size, Random.Range(0.38f, 0.78f), index % 3 == 0 ? scarlet : crimson, false);
        }
    }

    public void Bleed(Transform victim, Vector3 direction)
    {
        Vector3 wound = victim.position + Vector3.up * (victim.lossyScale.y * 1.35f) - direction * .24f;
        Blood(wound, direction);
        StartCoroutine(DripBlood(victim, victim.InverseTransformPoint(wound)));
    }

    private IEnumerator DripBlood(Transform victim, Vector3 wound)
    {
        var interval = new WaitForSeconds(.075f);
        for (int drop = 0; drop < 5; drop++)
        {
            yield return interval;
            if (victim == null)
                yield break;
            Vector3 point = victim.TransformPoint(wound) + Random.insideUnitSphere * .09f;
            Spawn(point, Vector3.down * .85f + Random.insideUnitSphere * .35f,
                Random.Range(.03f, .055f), .95f, scarlet, false);
        }
    }

    public void Dust(Vector3 point)
    {
        for (int index = 0; index < 15; index++)
        {
            Vector2 radial = Random.insideUnitCircle * 2.8f;
            Vector3 velocity = new(radial.x, Random.Range(0.35f, 1.1f), radial.y);
            Spawn(point, velocity, Random.Range(0.034f, 0.11f), Random.Range(0.25f, 0.55f), earth, true);
        }
    }

    private void Spawn(Vector3 point, Vector3 velocity, float size, float lifetime, Material material, bool dust)
    {
        Transform fragment = ForestHunterDetail.AddShape(transform, dust ? "Dust chip" : "Hit droplet",
            ForestStone.SharedMesh, material, Vector3.zero, new Vector3(size, size * 1.3f, size));
        fragment.position = point;
        fragments.Add(new Fragment { Object = fragment, Velocity = velocity, Lifetime = lifetime, Dust = dust });
    }

    private void Update()
    {
        float step = Time.deltaTime;
        for (int index = fragments.Count - 1; index >= 0; index--)
        {
            Fragment fragment = fragments[index];
            fragment.Lifetime -= step;
            if (fragment.Lifetime <= 0f)
            {
                Destroy(fragment.Object.gameObject);
                fragments.RemoveAt(index);
                continue;
            }
            Vector3 next = fragment.Object.position + fragment.Velocity * step;
            if (next.y <= ground + 0.025f)
            {
                next.y = ground + 0.025f;
                fragment.Velocity = Vector3.zero;
                Vector3 scale = fragment.Object.localScale;
                scale.y = Mathf.Min(scale.y, 0.022f);
                fragment.Object.localScale = scale;
            }
            else
            {
                fragment.Velocity += Vector3.down * ((fragment.Dust ? 4f : 7.5f) * step);
                fragment.Object.Rotate(110f * step, 170f * step, 50f * step);
            }
            fragment.Object.position = next;
            fragments[index] = fragment;
        }
    }

    private static Material Tint(Material source, Color color)
    {
        var material = new Material(source);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.38f);
        return material;
    }
}
