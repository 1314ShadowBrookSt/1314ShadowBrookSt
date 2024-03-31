using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dragAndDrop : MonoBehaviour
{

    public GameObject dragableObj;
    public GameObject dragPos;

    public float dropDistance;

    public bool isLocked;

    Vector2 draggableInitPos;

    // Start is called before the first frame update
    void Start()
    {
        //start position of draggable cards
        draggableInitPos = dragableObj.transform.position;
    }

    public void dragObject()
    {
        if(!isLocked)
            dragableObj.transform.position = Input.mousePosition;


    }
    public void dropObject()
    {
        float distance = Vector3.Distance(dragableObj.transform.position, dragPos.transform.position);

        if (distance < dropDistance)
        {
            isLocked = true;
            dragableObj.transform.position = dragPos.transform.position;
        }
        else
        {
            dragableObj.transform.position = draggableInitPos;
        }
            
    }

}
