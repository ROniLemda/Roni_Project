// כתובת הסוקט המרכזית של השרת
const SOCKET_URL = 'ws://localhost:5057/ws/status';

// פונקציה להפעלת חיבור הסוקט מול השרת
function startSocket() 
{
    // פתיחת קו הסוקט מול השרת
    const socket = new WebSocket(SOCKET_URL);

    // תפיסת האלמנטים מתוך הכרטיס שעל המסך
    const statusText = document.getElementById('serverStatusText');
    const statusIcon = document.getElementById('serverStatusIcon');

    // אירוע שמופעל ברגע שהחיבור לשרת נפתח בהצלחה 
    socket.onopen = function() 
    {
        // בדיקת הגנה לוודא שהאלמנט קיים במסך כדי למנוע קריסה של הדף
        if (statusText !== null) 
        {
            statusText.textContent = 'שרת מחובר'; // שינוי המילים שעל המסך לשרת מחובר
            statusText.className = 'status-text-online'; // צביעת המילים שעל המסך בצבע ירוק
        }
        if (statusIcon !== null) 
        {
            statusIcon.className = 'status-icon online'; // צביעת האייקון בצבע ירוק זוהר
        }

        // שליחת בדיקת דופק ראשונה לשרת
        socket.send('PING');
    };

    // פעולה שמופעלת ברגע שהשרת מתנתק נכבה או קורס
    socket.onclose = async function() 
    {
        // בדיקת הגנה שהאלמנט קיים בדף לפני עדכון המסך למצב מנותק
        if (statusText !== null) 
        {
            statusText.textContent = 'השרת מנותק'; // שינוי המילים שעל המסך להשרת מנותק
            statusText.className = 'status-text-offline'; // צביעת המילים שעל המסך בצבע אדום
        }
        if (statusIcon !== null) 
        {
            statusIcon.className = 'status-icon offline'; // צביעת האייקון בצבע אדום זוהר
        }

        // הקפצת הודעה למנהל על שינוי בסטטוס השרת עם כפתור לרענון הדף
        await Swal.fire({
            icon: 'info',
            title: 'חל שינוי בסטטוס השרת',
            text: 'הקשר מול השרת השתנה, לחץ על הכפתור כדי לרענן את נתוני הדף',
            confirmButtonText: 'רענן דף',
            confirmButtonColor: '#0052cc',
            allowOutsideClick: false // מונע סגירה של החלון בלחיצה על הרקע ומחייב לחיצה על כפתור הרענון
        });

        // רענון הדף לאחר לחיצה על הכפתור
        window.location.reload();
    };

    // אירוע שמופעל במקרה של שגיאת תקשורת בסוקט
    socket.onerror = function() 
    {
        // בדיקת הגנה שהאלמנט קיים בדף לפני עדכון המסך למצב מנותק
        if (statusText !== null) 
        {
            statusText.textContent = 'השרת מנותק'; // שינוי המילים שעל המסך להשרת מנותק
            statusText.className = 'status-text-offline'; // צביעת המילים שעל המסך בצבע אדום
        }
        if (statusIcon !== null) 
        {
            statusIcon.className = 'status-icon offline'; // צביעת האייקון בצבע אדום זוהר
        }
    };

    // שעון ששולח בדיקת דופק כל 5 שניות לוודא שהשרת עדיין חי
    setInterval(function() 
    {
        if (socket.readyState === WebSocket.OPEN) 
        {
            socket.send('PING');
        }
    }, 5000);
}

// הפעלת החיבור אוטומטית כשהדף עולה
startSocket();