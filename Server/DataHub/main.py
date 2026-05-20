from fastapi import FastAPI
from app import models
from app.database import engine
from app.routers import auth, inventory, admin, character, equipment, shop, enhance, storage

# 서버 시작 시 models.py에 정의된 테이블들을 DB에 자동 생성
models.Base.metadata.create_all(bind=engine)

app = FastAPI(title="Capstone Game API Server")

app.include_router(auth.router)       # /auth/*
app.include_router(inventory.router)  # /inventory/*
app.include_router(character.router)  # /character/*
app.include_router(equipment.router)  # /equipment/*
app.include_router(admin.router)      # /admin/*
app.include_router(shop.router)       # /shop/*
app.include_router(enhance.router)    # /enhance/*
app.include_router(storage.router)   # /storage/*

@app.get("/")
def root():
    return {"message": "Game API Server is running!"}
