from sqlalchemy import Column, Integer, String, Float, DateTime, Boolean, ForeignKey, UniqueConstraint
from sqlalchemy.sql import func
from sqlalchemy.orm import relationship
from app.database import Base


class User(Base):
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, index=True)
    login_id = Column(String(50), unique=True, index=True, nullable=False)
    hashed_password = Column(String(255), nullable=False)
    username = Column(String(50), nullable=False)
    created_at = Column(DateTime(timezone=True), server_default=func.now())

    is_banned = Column(Boolean, default=False, nullable=False)
    role = Column(String(20), default="user", nullable=False)
    last_login_at = Column(DateTime(timezone=True), nullable=True)

    inventory_items = relationship("Inventory", back_populates="owner")
    character = relationship("Character", back_populates="owner", uselist=False)  # 1:1
    equipped_items = relationship("EquippedItem", back_populates="owner")
    storage_items = relationship("Storage", back_populates="owner")


class Inventory(Base):
    __tablename__ = "inventories"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    item_id = Column(Integer, nullable=False)
    quantity = Column(Integer, default=1, nullable=False)
    enhance_level = Column(Integer, default=0, nullable=False)
    quickslot_index = Column(Integer, nullable=True)  # null=일반 인벤토리, 0~4=퀵슬롯 위치

    owner = relationship("User", back_populates="inventory_items")


class Character(Base):
    __tablename__ = "characters"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), unique=True, nullable=False)  # 유저당 1개

    # 현재 스탯 (기본값 = 기획 기본 수치)
    current_hp = Column(Integer, default=20, nullable=False)   # 기본 20 (10칸)
    gold = Column(Integer, default=0, nullable=False)

    # 마지막 위치 (게임 씬 재진입 시 복원)
    pos_x = Column(Float, default=0.0, nullable=False)
    pos_y = Column(Float, default=0.0, nullable=False)
    pos_z = Column(Float, default=0.0, nullable=False)

    owner = relationship("User", back_populates="character")


class EquippedItem(Base):
    __tablename__ = "equipped_items"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    slot_type = Column(String(20), nullable=False)
    item_id = Column(Integer, nullable=False)
    enhance_level = Column(Integer, default=0, nullable=False)

    # 같은 유저가 같은 슬롯에 중복 장착 불가
    __table_args__ = (UniqueConstraint("user_id", "slot_type"),)

    owner = relationship("User", back_populates="equipped_items")


class Storage(Base):
    __tablename__ = "storage"

    id           = Column(Integer, primary_key=True, index=True)
    user_id      = Column(Integer, ForeignKey("users.id"), nullable=False)
    item_id      = Column(Integer, nullable=False)
    quantity     = Column(Integer, default=1, nullable=False)
    enhance_level = Column(Integer, default=0, nullable=False)

    owner = relationship("User", back_populates="storage_items")


class ShopRotation(Base):
    __tablename__ = "shop_rotation"

    id         = Column(Integer, primary_key=True, index=True)
    item_id    = Column(Integer, nullable=False)
    shop_type  = Column(String(20), nullable=False, default="equipment")  # "equipment" | "consumable"
    is_random  = Column(Boolean, nullable=False, default=True)
    expires_at = Column(DateTime(timezone=True), nullable=True)  # 고정 아이템은 null
