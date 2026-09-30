#!/usr/bin/env python3
"""Наполняет стенд демо-данными через API: 10 сотрудников и 6 сессий оценки в разных состояниях.

Всё создаётся так же, как из интерфейса (снимок индикаторов, токены, письма в Mailpit),
поэтому отчёт и форма решения по демо-сессиям работают полностью.
Повторный запуск безопасен: существующие сотрудники и сотрудники с сессиями пропускаются.

Запуск на сервере:  scripts/seed-demo.py
Переменные: REVIEW_API_URL (http://localhost:15080), REVIEW_ADMIN_EMAIL (admin@example.com),
REVIEW_ADMIN_PASSWORD (если не задан — спросит).
"""
import getpass
import json
import os
import random
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

API = os.environ.get("REVIEW_API_URL", "http://localhost:15080").rstrip("/")

EMPLOYEES = [
    ("Алексей Смирнов", "a.smirnov@example.com", "E1"),
    ("Мария Кузнецова", "m.kuznetsova@example.com", "E2"),
    ("Дмитрий Волков", "d.volkov@example.com", "E3"),
    ("Анна Соколова", "a.sokolova@example.com", "E3"),
    ("Сергей Лебедев", "s.lebedev@example.com", "E4"),
    ("Екатерина Морозова", "e.morozova@example.com", "E4"),
    ("Иван Новиков", "i.novikov@example.com", "E5"),
    ("Ольга Павлова", "o.pavlova@example.com", "E6"),
    ("Никита Фёдоров", "n.fedorov@example.com", "E7"),
    ("Татьяна Орлова", "t.orlova@example.com", "E8"),
]

# Сессии оценки с разными результатами, чтобы посмотреть все варианты отчёта.
# current / target — средняя оценка окружения по индикаторам текущего и следующего грейда (0–3),
# self — насколько сотрудник оценивает себя выше (+) или ниже (−) окружения,
# submitted — сколько анкет сдать (None — все), decision — принять решение и закрыть сессию.
SESSIONS = [
    # Ждёт решения: текущий грейд подтверждён, до следующего далеко, переоценивает себя
    {"email": "s.lebedev@example.com", "type": "Transition", "current": 2.5, "target": 1.8, "self": 0.5},
    # Ждёт решения: подтверждение грейда Senior+
    {"email": "n.fedorov@example.com", "type": "Confirmation", "current": 2.5, "self": 0.3},
    # Ждёт решения: слабые результаты, сильно переоценивает себя
    {"email": "m.kuznetsova@example.com", "type": "Transition", "current": 1.6, "target": 1.0, "self": 1.1},
    # Закрыта: готова к следующему грейду, недооценивает себя → повышена до E4
    {"email": "a.sokolova@example.com", "type": "Transition", "current": 2.8, "target": 2.5, "self": -0.7,
     "decision": ("Promoted", "Уверенно закрывает индикаторы Middle, коллеги и лид отмечают самостоятельность.")},
    # Закрыта: грейд подтверждён, план развития к следующему
    {"email": "i.novikov@example.com", "type": "Transition", "current": 2.4, "target": 1.5, "self": 0.2,
     "decision": ("GradeConfirmed", "Текущий грейд подтверждён; к Senior не хватает системного дизайна и менторства.")},
    # В процессе: сдали не все — отчёт с пометкой «Промежуточные результаты»
    {"email": "o.pavlova@example.com", "type": "Transition", "current": 2.3, "target": 1.9, "self": 0.3, "submitted": 3},
]

# Вымышленные респонденты по ролям; Self добавляет сам бэкенд
RESPONDENTS = {
    "Manager": [("Павел Григорьев", "p.grigoriev@example.com")],
    "TeamLead": [("Роман Беляев", "r.belyaev@example.com")],
    "Peer": [("Юлия Зайцева", "y.zaitseva@example.com"), ("Кирилл Егоров", "k.egorov@example.com")],
    "Rck": [("Виктор Соловьёв", "v.solovyov@example.com")],
    "ItLeader": [("Елена Макарова", "e.makarova@example.com")],
}

COMMENTS = {
    0: "Не видел такого поведения за последние полгода",
    3: "Стабильно так работает, например на последнем релизе",
}


class Api:
    def __init__(self, base: str):
        self.base = base
        self.token: str | None = None

    def call(self, method: str, path: str, body=None):
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(self.base + path, data=data, method=method)
        request.add_header("Content-Type", "application/json")
        if self.token:
            request.add_header("Authorization", f"Bearer {self.token}")
        try:
            with urllib.request.urlopen(request) as response:
                raw = response.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as e:
            problem = e.read().decode(errors="replace")
            sys.exit(f"Ошибка {e.code} на {method} {path}: {problem}")
        except urllib.error.URLError as e:
            sys.exit(f"API недоступен ({self.base}): {e.reason}. Стенд запущен?")


def login(api: Api) -> dict:
    email = os.environ.get("REVIEW_ADMIN_EMAIL", "admin@example.com")
    password = os.environ.get("REVIEW_ADMIN_PASSWORD") or getpass.getpass(f"Пароль {email}: ")
    auth = api.call("POST", "/api/auth/login", {"email": email, "password": password})
    if auth["user"]["mustChangePassword"]:
        sys.exit("Сначала войдите в приложение и смените пароль администратора, затем запустите скрипт снова.")
    api.token = auth["accessToken"]
    return auth["user"]


def seed_employees(api: Api, manager_id: str) -> dict[str, dict]:
    track = next(t for t in api.call("GET", "/api/tracks") if t["isActive"])
    grades = {g["code"]: g["id"] for g in api.call("GET", "/api/grades")}
    existing = {e["email"]: e for e in api.call("GET", "/api/employees?includeArchived=true")}

    for name, email, grade in EMPLOYEES:
        if email in existing:
            print(f"  = {name} — уже есть")
            continue
        existing[email] = api.call("POST", "/api/employees", {
            "fullName": name, "email": email, "trackId": track["id"],
            "gradeId": grades[grade], "managerUserId": manager_id,
        })
        print(f"  + {name} ({grade})")
    return existing


def respondents_for(grade_code: str, rules: list[dict]) -> list[dict]:
    result = []
    for rule in (r for r in rules if r["gradeCode"] == grade_code and r["role"] != "Self"):
        # Коллег берём двое, если правило позволяет: так у индикаторов набирается кворум и есть разброс
        count = min(max(rule["minCount"], 2 if rule["role"] == "Peer" else 1), rule["maxCount"])
        result += [{"fullName": n, "email": e, "role": rule["role"]} for n, e in RESPONDENTS[rule["role"]][:count]]
    return result


def answer(rng: random.Random, mean: float) -> dict:
    if rng.random() < 0.04:
        return {"score": None, "notApplicable": True, "comment": None}
    score = max(0, min(3, round(rng.gauss(mean, 0.6))))
    return {"score": score, "notApplicable": False, "comment": COMMENTS.get(score)}


def fill_survey(api: Api, token: str, profile: dict, levels: dict[str, str], group_bias: dict[str, float],
                rng: random.Random, submit: bool) -> str:
    survey = api.call("GET", f"/api/surveys/{token}")
    shift = profile["self"] if survey["role"] == "Self" else rng.uniform(-0.3, 0.3)
    answers = []
    for group in survey["groups"]:
        for indicator in group["indicators"]:
            base = profile["current"] if levels.get(indicator["text"]) == "Current" else profile.get("target", 0)
            answers.append({"indicatorId": indicator["id"], **answer(rng, base + group_bias[group["name"]] + shift)})
    if submit:
        api.call("POST", f"/api/surveys/{token}/submit", {"answers": answers})
        return f"сдал анкету: {survey['respondentName']} ({survey['role']})"
    # Не сдавший начал заполнять: сохраняется черновик первой группы
    api.call("PUT", f"/api/surveys/{token}/draft", {"answers": answers[:len(survey["groups"][0]["indicators"])]})
    return f"заполняет:    {survey['respondentName']} ({survey['role']})"


def decide(api: Api, session_id: str, outcome: str, comment: str) -> None:
    session = api.call("GET", f"/api/assessment-sessions/{session_id}")
    report = api.call("GET", f"/api/assessment-sessions/{session_id}/report")
    new_grade = session["targetGrade"] if outcome == "Promoted" else session["currentGrade"]
    # План развития: зоны роста из отчёта (невыполненные индикаторы следующего грейда) и один свой пункт
    gaps = [i for i in report["indicators"] if i["level"] == "Target" and not i["isMet"] and not i["insufficientData"]]
    due = (datetime.now(timezone.utc) + timedelta(days=90)).date().isoformat()
    plan = [{"text": i["text"], "sessionIndicatorId": i["id"], "dueDate": due} for i in gaps[:3]]
    plan.append({"text": "Провести внутренний доклад для команды", "sessionIndicatorId": None, "dueDate": None})
    api.call("POST", f"/api/assessment-sessions/{session_id}/decision",
             {"outcome": outcome, "newGradeId": new_grade["id"], "comment": comment, "planItems": plan})


def seed_sessions(api: Api, employees: dict[str, dict]) -> None:
    rules = api.call("GET", "/api/grade-role-rules")
    deadline = (datetime.now(timezone.utc) + timedelta(days=14)).isoformat()

    for profile in SESSIONS:
        employee = employees[profile["email"]]
        if api.call("GET", f"/api/assessment-sessions?employeeId={employee['id']}"):
            print(f"  = {employee['fullName']} — сессия уже есть")
            continue

        rng = random.Random(profile["email"])
        session = api.call("POST", "/api/assessment-sessions", {
            "employeeId": employee["id"], "type": profile["type"], "deadlineAtUtc": deadline,
            "participants": respondents_for(employee["gradeCode"], rules),
        })
        preview = api.call("GET", f"/api/assessment-sessions/{session['id']}/survey-preview")
        levels = {i["text"]: i["levelKind"] for g in preview["groups"] for i in g["indicators"]}
        # Сильные и слабые стороны: у каждой группы компетенций свой сдвиг — паутинка получается неровной
        group_bias = {g["name"]: rng.uniform(-0.5, 0.4) for g in preview["groups"]}

        links = api.call("POST", f"/api/assessment-sessions/{session['id']}/launch")
        print(f"  + {employee['fullName']}: {profile['type']}, {preview['indicatorCount']} индикаторов")
        submitted = profile.get("submitted") or len(links)
        for n, link in enumerate(links):
            token = link["url"].rsplit("/", 1)[-1]
            print(f"      {fill_survey(api, token, profile, levels, group_bias, rng, n < submitted)}")

        if decision := profile.get("decision"):
            decide(api, session["id"], *decision)
        status = api.call("GET", f"/api/assessment-sessions/{session['id']}")["status"]
        print(f"      статус сессии: {status}")


def main() -> None:
    api = Api(API)
    user = login(api)
    print("Сотрудники:")
    employees = seed_employees(api, user["id"])
    print("Сессии:")
    seed_sessions(api, employees)
    print("Готово.")


if __name__ == "__main__":
    main()
