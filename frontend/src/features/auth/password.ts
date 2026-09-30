import { z } from 'zod'

/** Те же правила, что у ASP.NET Identity на сервере. */
export const passwordSchema = z
  .string()
  .min(8, 'Минимум 8 символов')
  .regex(/\d/, 'Нужна хотя бы одна цифра')
  .regex(/\p{Ll}/u, 'Нужна строчная буква')
  .regex(/\p{Lu}/u, 'Нужна заглавная буква')
