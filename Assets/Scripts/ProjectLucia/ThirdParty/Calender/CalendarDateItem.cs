using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucia.ThirdParty.Calender
{
    public class CalendarDateItem : MonoBehaviour {

        public void OnDateItemClick()
        {
            CalendarController._calendarInstance.OnDateItemClick(gameObject.GetComponentInChildren<Text>().text);
        }
    }
}
