import java.util.Scanner;

public class GilbDemo
{
    public static void main(String[] args)
    {
        Scanner in = new Scanner(System.in);
        System.out.print("Введите количество элементов: ");
        int n = in.nextInt();
        int[] a = new int[n];
        int sum = 0;

        // цикл for
        for (int i = 0; i < n; i++) {
            a[i] = in.nextInt();
            sum += a[i];
        }

        int positive = 0;
        int negative = 0;

        // цикл for-each + if / else if + вложенный if
        for (int x : a) {
            if (x > 0) {
                positive++;
                if (x > 100) {
                    System.out.println("Большое число: " + x);
                }
            } else if (x < 0) {
                negative++;
            }
        }

        // цикл while + if без else
        int k = 0;
        while (k < n) {
            if (a[k] % 2 == 0) {
                System.out.println(a[k]);
            }
            k++;
        }

        // цикл do-while
        int m = n;
        do {
            m--;
        } while (m > 0);

        // if-else
        if (sum > 0) {
            System.out.println("Сумма положительная");
        } else {
            System.out.println("Сумма неположительная");
        }

        // оператор множественного выбора
        System.out.print("Команда (1-3): ");
        int cmd = in.nextInt();
        switch (cmd) {
            case 1:
                System.out.println(sum);
                break;
            case 2:
                System.out.println(positive);
                break;
            case 3:
                System.out.println(negative);
                break;
            default:
                System.out.println("Неизвестная команда");
        }
    }
}
