from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.lib.utils import simpleSplit


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "pdf" / "AI_활용_기술_문서.pdf"
FONT = r"C:\Windows\Fonts\malgun.ttf"
BOLD = r"C:\Windows\Fonts\malgunbd.ttf"
W, H = A4
M = 48
NAVY = colors.HexColor("#12263A")
BLUE = colors.HexColor("#1877B9")
TEAL = colors.HexColor("#10A6A1")
PALE = colors.HexColor("#EAF4F7")
GRAY = colors.HexColor("#5C6670")
LINE = colors.HexColor("#CAD6DE")


def setup_fonts():
    pdfmetrics.registerFont(TTFont("KR", FONT))
    pdfmetrics.registerFont(TTFont("KR-Bold", BOLD))


def txt(c, x, y, text, size=10, color=NAVY, font="KR"):
    c.setFont(font, size)
    c.setFillColor(color)
    c.drawString(x, y, text)


def paragraph(c, x, y, text, width, size=10, leading=15, color=NAVY, font="KR"):
    c.setFillColor(color)
    c.setFont(font, size)
    lines = []
    for p in text.split("\n"):
        lines.extend(simpleSplit(p, font, size, width) or [""])
    for line in lines:
        c.drawString(x, y, line)
        y -= leading
    return y


def header(c, page, kicker, title, subtitle):
    c.setFillColor(NAVY)
    c.rect(0, H - 30, W, 30, fill=1, stroke=0)
    txt(c, M, H - 21, "IN THE ARENA | AI UTILIZATION TECHNICAL DOCUMENT", 8, colors.white, "KR-Bold")
    txt(c, W - 70, H - 21, f"{page:02d}", 8, colors.white, "KR-Bold")
    txt(c, M, H - 58, kicker, 9, TEAL, "KR-Bold")
    txt(c, M, H - 89, title, 22, NAVY, "KR-Bold")
    paragraph(c, M, H - 109, subtitle, W - 2*M, 9.5, 14, GRAY)
    c.setStrokeColor(LINE)
    c.line(M, H - 137, W - M, H - 137)


def section(c, y, number, title):
    c.setFillColor(BLUE)
    c.circle(M + 9, y - 2, 9, fill=1, stroke=0)
    txt(c, M + 5.5, y - 5.5, str(number), 8, colors.white, "KR-Bold")
    txt(c, M + 25, y - 6, title, 13, NAVY, "KR-Bold")
    return y - 27


def box(c, x, y, w, h, title, body, fill=colors.white):
    c.setFillColor(fill)
    c.setStrokeColor(LINE)
    c.roundRect(x, y-h, w, h, 8, fill=1, stroke=1)
    txt(c, x+12, y-19, title, 10, NAVY, "KR-Bold")
    paragraph(c, x+12, y-37, body, w-24, 8.4, 12, GRAY)


def footer(c, note):
    c.setStrokeColor(LINE)
    c.line(M, 34, W-M, 34)
    txt(c, M, 20, note, 7.2, GRAY)
    txt(c, W-110, 20, "작성일 2026.08.10", 7.2, GRAY)


def flow(c, y):
    labels = ["기획·개발\n요청", "코드·위키\n탐색", "변경 영향\n분석", "구현·테스트\n검증", "위키·이력\n갱신"]
    x = M
    bw, bh, gap = 88, 46, 9
    for i, label in enumerate(labels):
        c.setFillColor(PALE if i % 2 == 0 else colors.white)
        c.setStrokeColor(BLUE)
        c.roundRect(x, y-bh, bw, bh, 7, fill=1, stroke=1)
        for j, line in enumerate(label.split("\n")):
            txt(c, x+17, y-19-j*13, line, 8.5, NAVY, "KR-Bold")
        if i < len(labels)-1:
            c.setStrokeColor(TEAL)
            c.setFillColor(TEAL)
            c.line(x+bw+2, y-23, x+bw+gap-4, y-23)
            c.line(x+bw+gap-8, y-19, x+bw+gap-4, y-23)
            c.line(x+bw+gap-8, y-27, x+bw+gap-4, y-23)
        x += bw + gap


def table(c, x, y, widths, headers, rows, row_heights):
    h = 23
    c.setStrokeColor(LINE)
    c.setFillColor(NAVY)
    c.rect(x, y-h, sum(widths), h, fill=1, stroke=1)
    left = x
    for head, w in zip(headers, widths):
        txt(c, left+5, y-15, head, 7.2, colors.white, "KR-Bold")
        left += w
    top = y-h
    for row, rh in zip(rows, row_heights):
        left = x
        c.setFillColor(colors.white)
        c.rect(x, top-rh, sum(widths), rh, fill=1, stroke=1)
        for cell, w in zip(row, widths):
            c.setStrokeColor(LINE)
            c.line(left, top, left, top-rh)
            paragraph(c, left+5, top-12, cell, w-10, 6.5, 8.3, NAVY)
            left += w
        c.line(x+sum(widths), top, x+sum(widths), top-rh)
        top -= rh
    return top


def build():
    setup_fonts()
    c = canvas.Canvas(str(OUT), pagesize=A4)
    c.setTitle("AI 활용 기술 문서 | In the Arena")
    c.setAuthor("In the Arena 개발팀")

    # Page 1
    header(c, 1, "AI UTILIZATION OVERVIEW", "AI 활용 기술 문서", "프로젝트: In the Arena - Betting Comes Before the Blade | Unity 기반 전략·베팅 게임")
    y = H - 163
    y = section(c, y, 1, "활용 원칙과 범위")
    y = paragraph(c, M, y, "본 프로젝트는 AI를 자동 제작 주체가 아니라 기획 지식의 구조화, 개발 보조, 아이콘 시안 생성에 활용하였다. 게임 규칙·콘텐츠·밸런스·UI·코드의 최종 설계와 채택, 구현 검증 및 출시는 개발자가 책임진다. AI 산출물은 기존 구현과 테스트, 플레이 결과를 기준으로 검토한 뒤에만 반영하였다.", W-2*M, 9.4, 15, NAVY)
    y -= 8
    box(c, M, y, 156, 75, "LLM 기반 위키 아카이브", "게임 지식의 구조화\n영향 범위 점검", PALE)
    box(c, M+170, y, 156, 75, "OpenAI Codex", "코드 탐색·구현 보조\n테스트·문서 점검", colors.white)
    box(c, M+340, y, 156, 75, "ChatGPT 이미지 생성", "아이콘 시안 생성\n선정·후편집·배치", PALE)
    y -= 99
    y = section(c, y, 2, "LLM 위키 응용: 자동 게임 기획 아카이브")
    y = paragraph(c, M, y, "게임 규칙, 유닛·스테이지 데이터, 라운드와 베팅, 자동 전투, 전투 아이템, 저장·보상, 화면 흐름과 변경 이력을 Markdown/Obsidian 위키로 관리하였다. 코드·씬·테스트를 구현 근거로 연결하여, 기능 수정 시 문서와 현재 구현의 불일치를 빠르게 확인하도록 구성하였다.", W-2*M, 9.3, 15)
    y -= 11
    flow(c, y)
    y -= 65
    c.setFillColor(colors.HexColor("#F7FAFB"))
    c.setStrokeColor(LINE)
    c.roundRect(M, y-64, W-2*M, 64, 8, fill=1, stroke=1)
    txt(c, M+13, y-19, "주요 지시 사항", 9.5, BLUE, "KR-Bold")
    paragraph(c, M+13, y-37, "“현재 구현을 우선 근거로 삼고, 근거가 부족하면 확정하지 말고 review로 표시한다. 기능·규칙·콘텐츠·UI 흐름이 변경되면 관련 위키 및 변경 이력의 영향도 함께 점검한다.”", W-2*M-26, 8.7, 13, NAVY)
    footer(c, "근거: 프로젝트 위키의 게임 개요·시스템·콘텐츠·기술·변경 이력 문서 및 Unity 프로젝트 구조")
    c.showPage()

    # Page 2
    header(c, 2, "CODEX ASSISTED DEVELOPMENT", "Codex 활용 내역", "요구사항, 코드, 테스트, 프로젝트 규칙을 함께 참조하여 반복 개발 작업을 보조")
    y = H - 163
    y = section(c, y, 3, "활용 목적 및 입력 정보")
    rows = [
        ["활용 목적", "Unity C# 구현 보조, 기존 코드 탐색, 버그 원인 분석, 테스트 작성·검증, 위키 영향 점검"],
        ["입력 정보", "기능 요구사항, Unity 프로젝트 코드·씬·에셋, 테스트, 구현 위키, 저장소 작업 규칙"],
        ["산출물", "코드 수정 제안 또는 구현, 테스트·검증 결과, 변경 요약, 위키 반영 후보"],
    ]
    y = table(c, M, y, [92, W-2*M-92], ["항목", "내용"], rows, [45, 45, 45]) - 23
    y = section(c, y, 4, "작업 흐름과 통제")
    box(c, M, y, 145, 92, "1. 조사", "요청과 연관된 코드, 테스트, 위키 문서를 탐색한다.", PALE)
    box(c, M+157, y, 145, 92, "2. 제안·구현", "현재 구조와 규칙을 유지하는 범위에서 변경안을 작성한다.", colors.white)
    box(c, M+314, y, 145, 92, "3. 검증·반영", "테스트와 플레이 검증 후 개발자가 결과를 확정한다.", PALE)
    y -= 116
    c.setFillColor(colors.HexColor("#F7FAFB"))
    c.setStrokeColor(LINE)
    c.roundRect(M, y-85, W-2*M, 85, 8, fill=1, stroke=1)
    txt(c, M+13, y-19, "대표 지시 예시", 9.5, BLUE, "KR-Bold")
    paragraph(c, M+13, y-38, "“기존 동작과 관련 테스트를 먼저 조사한다. 회귀 위험이 있는 변경은 테스트로 확인한다. 게임 규칙·UI·데이터 의미가 바뀌면 위키 영향도 함께 점검하고, 확정되지 않은 내용은 추정하지 않는다.”", W-2*M-26, 8.7, 13, NAVY)
    y -= 111
    y = section(c, y, 5, "역할 분담 및 출처")
    y = paragraph(c, M, y, "Codex는 소프트웨어 개발 작업의 설계·구현·리뷰를 지원하는 개발 에이전트로 사용하였다. 본 프로젝트에서는 AI의 제안과 생성 결과를 개발자가 검토하고, 실제 게임 플레이와 테스트로 확인한 뒤 반영했다.", W-2*M, 9.2, 15)
    txt(c, M, y-7, "공식 출처  OpenAI Codex: https://openai.com/codex/", 8.3, BLUE, "KR-Bold")
    footer(c, "참고: OpenAI Codex 공식 제품 소개 페이지 (접속일 2026.08.10)")
    c.showPage()

    # Page 3
    header(c, 3, "ICON GENERATION & ATTRIBUTION", "ChatGPT 아이콘 생성 및 외부 출처", "AI 생성물과 외부 에셋을 분리 기록하고, 확인되지 않은 라이선스는 확정 표기하지 않음")
    y = H - 163
    y = section(c, y, 6, "ChatGPT 이미지 생성: 아이콘 시안")
    y = paragraph(c, M, y, "아이콘이 필요한 기능의 역할과 시각 요소를 자연어로 지시해 시안을 생성하였다. 프롬프트에는 UI 용도, 픽셀 스타일, 해상도·비율, 투명 배경 여부, 제외할 요소를 포함했다. 생성 결과는 후보 중에서 개발자가 선택하고, 게임 UI의 가독성과 톤에 맞게 후편집·배치했다.", W-2*M, 9.2, 15)
    y -= 8
    box(c, M, y, W-2*M, 76, "대표 프롬프트 형식 (비공개 정보 제거)", "“[기능명]용 게임 UI 아이콘. 일관된 픽셀 아트 스타일, 정사각형 비율, 투명 배경, 작은 크기에서도 식별 가능한 실루엣. 텍스트·워터마크·실존 상표·복잡한 배경은 제외.”", PALE)
    y -= 100
    y = section(c, y, 7, "외부 에셋 및 오픈소스 출처")
    rows = [
        ["오픈소스", "DOTween / DOTween Pro", "UI·연출 애니메이션", "Demigiant\ndotween.demigiant.com", "DOTween 라이선스 / Pro 좌석 라이선스", "프로젝트 내 사용"],
        ["오픈소스", "MCP for Unity", "Unity 편집기 연동", "github.com/CoplayDev/unity-mcp", "MIT", "Package Manifest 등록"],
        ["외부 에셋", "Pixel Sprite Effects pack", "전투 이펙트", "원 구매·다운로드 페이지와 대조 필요", "확인 중", "프로젝트 내 사용"],
        ["외부 에셋", "RPG Icons Pixel Art", "아이콘 리소스", "원 구매·다운로드 페이지와 대조 필요", "확인 중", "프로젝트 내 사용"],
        ["AI 생성", "ChatGPT 생성 아이콘", "해당 UI 기능", "ChatGPT 생성 기록", "생성 서비스 약관 확인", "선택·후편집·배치"],
    ]
    y = table(c, M, y, [44, 80, 69, 105, 92, 63], ["구분", "명칭", "사용 위치", "출처", "라이선스", "수정 여부"], rows, [51, 51, 51, 51, 51]) - 17
    c.setFillColor(colors.HexColor("#FFF7E5"))
    c.setStrokeColor(colors.HexColor("#E7C978"))
    c.roundRect(M, y-53, W-2*M, 53, 8, fill=1, stroke=1)
    txt(c, M+13, y-18, "제출 전 확인", 9.5, colors.HexColor("#A46B00"), "KR-Bold")
    paragraph(c, M+13, y-35, "Pixel Sprite Effects pack 및 RPG Icons Pixel Art은 현재 프로젝트 폴더명만으로 원 배포처·구매 기록·정확한 라이선스를 확정할 수 없다. 제출 전 구매/다운로드 이력과 원 페이지를 대조해 표의 ‘확인 중’ 항목을 실제 URL과 라이선스로 교체한다.", W-2*M-26, 7.8, 11, NAVY)
    footer(c, "출처 확인: Packages/manifest.json, Assets/Plugins/Demigiant/readme_DOTweenPro.txt, 에셋 폴더명")
    c.save()


if __name__ == "__main__":
    OUT.parent.mkdir(parents=True, exist_ok=True)
    build()
    print(OUT)
