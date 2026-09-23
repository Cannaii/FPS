using UnityEngine;

namespace cowsins
{
    [CreateAssetMenu(fileName = "newBulletsInventoryItem", menuName = "COWSINS/New Bullet Identifier", order = 1)]
    public class BulletTypeIdentifier_SO : Item_SO
    {
        public enum BulletType { Custom, Universal }

        public BulletType bulletType;
    }
}