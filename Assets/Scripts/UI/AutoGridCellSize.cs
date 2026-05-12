using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class AutoGridCellSize : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private int columns = 4;
    [SerializeField] private float aspect = 236f / 191f;

    private void OnEnable() => Resize();
    private void OnRectTransformDimensionsChange() => Resize();

    private void Resize()
    {
        if (!viewport || !grid) return;

        float width = viewport.rect.width;
        float spacing = grid.spacing.x * (columns - 1);
        float padding = grid.padding.left + grid.padding.right;

        float cellWidth = (width - spacing - padding) / columns;
        float cellHeight = cellWidth * aspect;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(cellWidth, cellHeight);
    }
}
