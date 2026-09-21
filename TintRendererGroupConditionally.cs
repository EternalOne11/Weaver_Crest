using System;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace Weaver_Crest.Components;

internal class TintRendererGroupConditionally : TintRendererGroup {
	public Func<bool> Condition { get; set; } = () => true;
	private new void UpdateTint() {
		if (Condition())
			base.UpdateTint();
	}
	private new void Update() => UpdateTint();
	private new void OnEnable() {
		sprites.Clear();
		others.Clear();
		particles.Clear();
		meshRenderers.Clear();
		GetComponentsInChildrenRecursively(transform);
		if (meshRenderers.Count > 0)
			block = new MaterialPropertyBlock();
		UpdateTint();
	}
}
